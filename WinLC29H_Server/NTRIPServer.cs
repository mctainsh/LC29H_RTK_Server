using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace WinLC29H_Server
{
	public class NTRIPServer
	{
		private const int SOCKET_RETRY_INTERVAL_SECONDS = 300;
		private const int AVERAGE_SEND_TIMERS = 10;
		private const int SOCKET_IN_BUFFER_MAX = 1024;

		private int _index;
		private List<int> _sendMicroSeconds = new List<int>();
		private string _sAddress;
		private int _port;
		private string _sCredential;
		private string _sPassword;
		private TcpClient _client = new TcpClient();
		private NetworkStream _stream;
		private bool _wasConnected = false;
		private string _status;
		private DateTime _wifiConnectTime;
		private int _reconnects = 0;
		private int _packetsSent = 0;
		private long _maxSendTime = 0;

		/// <summary>
		/// Queue control
		/// </summary>
		Object _queueLock = new Object();
		List<byte[]> _outboundQueue = new List<byte[]>();

		public NTRIPServer(int index)
		{
			_index = index;
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
				if (_outboundQueue.Count > 0)
				{
					byte[] byteArray;
					lock (_queueLock)
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
		}

		public void Loop(byte[] pBytes, int length)
		{
			if (_port < 1 || string.IsNullOrEmpty(_sAddress))
				return;

			if (_client.Connected)
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
			if (length < 1)
				return;

			while (_sendMicroSeconds.Count >= AVERAGE_SEND_TIMERS)
				_sendMicroSeconds.RemoveAt(0);

			long startT = DateTime.Now.Ticks;
			_stream.Write(pBytes, 0, length);
			long time = DateTime.Now.Ticks - startT;

			if (_maxSendTime == 0)
				_maxSendTime = time;
			else
				_maxSendTime = Math.Max(_maxSendTime, time);

			_sendMicroSeconds.Add((int)(length * 8 * 1000 / Math.Max(1L, time)));
			_wifiConnectTime = DateTime.Now;
			_packetsSent++;
		}

		private void ConnectedProcessingReceive()
		{
			if (_stream.DataAvailable)
			{
				byte[] buffer = new byte[SOCKET_IN_BUFFER_MAX];
				int bytesRead = _stream.Read(buffer, 0, buffer.Length);
				Log.Ln("RECV. " + _sAddress + "\r\n" + HexAsciDump(buffer, bytesRead));
			}
		}

		public int AverageSendTime()
		{
			if (_sendMicroSeconds.Count < 1)
				return 0;
			int total = 0;
			foreach (int n in _sendMicroSeconds)
				total += n;
			return total / _sendMicroSeconds.Count;
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
			_client.NoDelay = false;

			try
			{
				_client.Connect(_sAddress, _port);
				_stream = _client.GetStream();
				_client.NoDelay = true;
			}
			catch (Exception ex)
			{
				Log.Ln($"E500 - RTK {_sAddress} Not connected. ({ex.Message})");
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
			_stream.Write(data, 0, data.Length);
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
			lock (_queueLock)
			{
				_outboundQueue.Add(byteArray);
			}
		}
	}
}