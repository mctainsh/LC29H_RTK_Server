using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace WinLC29H_Server
{
	public class NTRIPServer
	{
		private const int SOCKET_RETRY_INTERVAL_SECONDS = 300;
		private const int AVERAGE_SEND_TIMERS = 3600;
		private const int SOCKET_IN_BUFFER_MAX = 1024;

		private int _index;

		private string _sAddress;
		private int _port;
		private string _sCredential;
		private string _sPassword;

		/// <summary>
		/// Socket
		/// </summary>
		private Socket _client = null;

		private bool _wasConnected = false;
		private string _status;
		private DateTime _wifiConnectTime;
		private int _reconnects = 0;
		private int _packetsSent = 0;
		private double _maxSendTime = 0;

		/// <summary>
		/// Send time calculations
		/// </summary>
		List<double> _sendMicroSeconds = new List<double>();

		/// <summary>
		/// Queue control
		/// </summary>
		//Object _queueLock = new Object();
		List<byte[]> _outboundQueue = new List<byte[]>();

		public NTRIPServer(int index)
		{
			_index = index;
		}

		/// <summary>
		/// Print socket details
		/// </summary>
		override public string ToString()
		{
			return $"{_sAddress} - {_status}\r\n" +
			$"\tReconnects {_reconnects}\r\n" +
			$"\tSent       {_packetsSent:N0}\r\n" +
			$"\tMax Send   {_maxSendTime} ms\r\n" +
			$"\tSpeed      {AverageSendTime():N0} kbps";
			_maxSendTime = 0;
		}

		public bool LoadSettings()
		{
			string fileName = $"Caster{_index}.txt";
			if (!File.Exists(fileName))
				return false;
			Log.Ln("Processing " + fileName);
			var llText = File.ReadAllText(fileName);

			var parts = llText.Split('\n');
			if (parts.Length > 3)
			{
				_sAddress = parts[0].Trim('\r');
				_port = int.Parse(parts[1].Trim('\r'));
				_sCredential = parts[2].Trim('\r');
				_sPassword = parts[3].Trim('\r');
				Log.Ln($" - Recovered\r\n\t Address  : {_sAddress}\r\n\t Port     : {_port}\r\n\t Mid/Cred : {_sCredential}\r\n\t Pass     : {_sPassword}");
			}
			else
			{
				Log.Ln($" - E341 - Cannot read saved Server settings {llText}");
			}

			// Start the main processing thread
			new System.Threading.Thread(() => Loop()).Start();
			return true;
		}

		/// <summary>
		/// Process the socket connection in a seperate thread
		/// </summary>
		void Loop()
		{
			while (true)
			{
				try
				{
					if (_outboundQueue.Count > 0)
					{
						byte[] byteArray;
						lock (_outboundQueue)
						{
							byteArray = _outboundQueue[0];
							_outboundQueue.RemoveAt(0);
						}
						Loop(byteArray, byteArray.Length);
					}
					else
					{
						System.Threading.Thread.Sleep(1);
					}
				}
				catch (Exception ex)
				{
					Log.Ln($"E500 - RTK {_sAddress} Loop error. {ex.ToString().Replace("\n", "\n\t\t")}");
					System.Threading.Thread.Sleep(1_000);
					CloseSocket();
				}
			}
		}

		public void Loop(byte[] pBytes, int length)
		{
			if (_client?.Connected == true)
			{
				ConnectedProcessing(pBytes, length);
			}
			else
			{
				_wasConnected = false;
				_status = "Disconn...";
				Reconnect();
			}
		}

		/// <summary>
		/// Processing of the connected socket involves sending the new data
		/// </summary>
		private void ConnectedProcessing(byte[] pBytes, int length)
		{
			if (!_wasConnected)
			{
				_reconnects++;
				_status = "Connected";
				_wasConnected = true;
			}

			ConnectedProcessingSend(pBytes, length);
			ConnectedProcessingReceive();
		}

		/// <summary>
		/// Send the data to the connected socket
		/// </summary>
		private void ConnectedProcessingSend(byte[] pBytes, int length)
		{
			// Anything to send
			if (length < 1)
				return;

			//var startT = DateTime.Now;
			var stopwatch = Stopwatch.StartNew();

			// Clean up the send timer queue
			lock (_sendMicroSeconds)
			{
				while (_sendMicroSeconds.Count >= AVERAGE_SEND_TIMERS)
					_sendMicroSeconds.RemoveAt(0);
			}

			try
			{
				// Keep a history of send timers
				var sent = _client.Send(pBytes,length, SocketFlags.None);
				if (sent != length)
					throw new Exception($"Incomplete send {length} != {sent}");

				//var time = (DateTime.Now - startT).TotalMilliseconds;
				stopwatch.Stop();
				var time = stopwatch.Elapsed.TotalMilliseconds;

				if (_maxSendTime == 0)
					_maxSendTime = time;
				else
					_maxSendTime = Math.Max(_maxSendTime, time);

				if (time != 0)
				{
					lock (_sendMicroSeconds)
					{
						_sendMicroSeconds.Add((length*8.0)/time);
					}
				}
				_wifiConnectTime = DateTime.Now;
				_packetsSent++;
			}
			catch (Exception ex)
			{
				Log.Ln($"E500 - RTK {_sAddress} Not connected. ({ex.ToString()})");
				CloseSocket();
				_wasConnected = false;
			}
		}

		private void CloseSocket()
		{
			try { _client?.Close(); } catch { }
			_client = null;
		}

		/// <summary>
		/// Process any received data
		/// </summary>
		private void ConnectedProcessingReceive()
		{
			if (_client is null || _client.Available < 1)
				return;

			var buffer = new byte[_client.Available];
			var bytesRead = _client.Receive(buffer, _client.Available, SocketFlags.None);
			string str = "RECV. " + _sAddress + "\r\n";
			if (IsAllAscii(buffer, bytesRead))
				str += Encoding.ASCII.GetString(buffer, 0, bytesRead);
			else
				str += HexAsciDump(buffer, bytesRead);
			Log.Ln(str.Replace("\n", "\n\t\t"));
		}

		/// <summary>
		/// Safely read progress
		/// </summary>
		public double AverageSendTime()
		{
			lock (_sendMicroSeconds)
			{
				if (_sendMicroSeconds.Count < 1)
					return 0;
				double total = 0;
				foreach (double n in _sendMicroSeconds)
					total += n;
				return total / _sendMicroSeconds.Count;
			}
		}

		/// <summary>
		/// Reconnect. But only if we have not tried for a while
		/// </summary>
		/// <returns>False if retry was not attempted</returns>
		private bool Reconnect()
		{
			if ((DateTime.Now - _wifiConnectTime).TotalSeconds < SOCKET_RETRY_INTERVAL_SECONDS)
				return false;

			_wifiConnectTime = DateTime.Now;

			Log.Ln($"RTK Connecting to {_sAddress} : {_port}");

			try
			{
				_client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				_client.Connect(_sAddress, _port);
			}
			catch (Exception ex)
			{
				Log.Ln($"E500 - RTK {_sAddress} Not connected. ({ex.Message})");
				CloseSocket();
				return false;
			}

			Log.Ln($"Connected {_sAddress} OK.");

			if (!WriteText($"SOURCE {_sPassword} {_sCredential}\r\n"))
				return false;
			if (!WriteText("Source-Agent: NTRIP UM98/ESP32_T_Display_SX\r\n"))
				return false;
			if (!WriteText("STR: \r\n"))
				return false;
			if (!WriteText("\r\n"))
				return false;
			return true;
		}

		private bool WriteText(string str)
		{
			if (str is null)
				return true;

			string message = $"    -> '{str}'";
			ReplaceCrLfEncode(ref message);
			Log.Ln(message);

			byte[] data = Encoding.ASCII.GetBytes(str);
			_client.Send(data, data.Length, SocketFlags.None);
			return true;
		}

		/// <summary>
		/// Dump the data as a line of hex
		/// </summary>
		private string HexAsciDump(byte[] data, int length)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < length; i++)
				sb.AppendFormat("{0:X2} ", data[i]);
			return sb.ToString();
		}

		/// <summary>
		/// Check if all the charactors are printable including CR and LF
		/// </summary>
		private bool IsAllAscii(byte[] data, int length)
		{
			if (length < 1)
				return false;
			for (int i = 0; i < length; i++)
			{
				var c = (char)data[i];
				if (c == '\r' || c == '\n')
					continue;
				if (c < 32 || c > 126)
					return false;
			}
			return true;
		}

		void ReplaceCrLfEncode(ref string str)
		{
			string crlf = "\r\n";
			string newline = "\\r\\n";
			str = str.Replace(crlf, newline);
		}

		/// <summary>
		/// Add this to the outbouind que for sending
		/// </summary>
		internal void Send(byte[] byteArray)
		{
			lock (_outboundQueue)
			{
				_outboundQueue.Add(byteArray);
			}
		}
	}
}