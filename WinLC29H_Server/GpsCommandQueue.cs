using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;

namespace WinLC29H_Server
{
	public class GpsCommandQueue
	{
		private List<string> _strings = new List<string>();
		private int _timeSent = 0;
		private string _deviceType;
		private string _deviceFirmware = "UNKNOWN";
		private string _deviceSerial = "UNKNOWN";
		private SerialPort _port;

		public GpsCommandQueue(SerialPort port)
		{
			_port = port;
		}

		public string GetDeviceType() => _deviceType;
		public string GetDeviceFirmware() => _deviceFirmware;
		public string GetDeviceSerial() => _deviceSerial;

		public void CheckForVersion(string str)
		{
			if (!str.StartsWith("#VERSION"))
				return;

			var sections = str.Split(';');
			if (sections.Length < 1)
			{
				LogX($"DANGER 301 : Unknown sections '{str}' Detected");
				return;
			}

			var parts = sections[1].Split(',');
			if (parts.Length < 5)
			{
				LogX($"DANGER 302 : Unknown split '{str}' Detected");
				return;
			}
			_deviceType = parts[0];
			_deviceFirmware = parts[1];
			var serialPart = parts[3].Split('-');
			_deviceSerial = serialPart[0];

			string command = "CONFIG SIGNALGROUP 3 6"; // Assume for UM982
			if (_deviceType == "UM982")
			{
				LogX("UM982 Detected");
			}
			else if (_deviceType == "UM980")
			{
				LogX("UM980 Detected");
				command = "CONFIG SIGNALGROUP 2"; // for UM980
			}
			else
			{
				LogX($"DANGER 303 Unknown Device '{_deviceType}' Detected in {str}");
			}
			_strings.Add(command);
		}

		public bool VerifyChecksum(string str)
		{
			int asteriskPos = str.LastIndexOf('*');
			if (asteriskPos == -1 || asteriskPos + 3 > str.Length)
			{
				LogX($"ERROR : GPS Checksum error in {str}");
				return false;
			}

			string data = str.Substring(1, asteriskPos - 1);
			string providedChecksumStr = str.Substring(asteriskPos + 1, 2);

			if (!uint.TryParse(providedChecksumStr, System.Globalization.NumberStyles.HexNumber, null, out uint providedChecksum))
			{
				LogX($"ERROR : GPS Checksum error in {str}");
				return false;
			}

			byte calculatedChecksum = CalculateChecksum(data);

			return calculatedChecksum == (byte)providedChecksum;
		}

		public bool IsCommandResponse(string str)
		{
			if (!_strings.Any())
				return false;

			if (!VerifyChecksum(str))
			{
				LogX($"ERROR : GPS Checksum error in {str}");
				return false;
			}

			if (str.StartsWith("$G"))
				return false;

			string match = "$" + _strings.First();

			if (str.StartsWith("$PQTMCFGSVIN,OK"))
			{
				LogX($"GPS Configured : {str}");
			}
			else if (str.StartsWith("$PQTMCFGSVIN"))
			{
				LogX($"ERROR GPS NOT Configured : {str}");
			}
			else if (str.StartsWith("$PAIR001"))
			{
				if (str.Length < 15)
				{
					LogX($"ERROR : PAIR001 too short {str}");
					return false;
				}
				if (match.Length < 10)
				{
					LogX($"ERROR : {str} too short for PAIR");
					return false;
				}
				if (match[5] != str[9] || match[6] != str[10] || match[7] != str[11])
				{
					LogX($"ERROR : PAIR001 mismatch {str} and {match}");
					return false;
				}
			}
			else
			{
				if (!str.StartsWith(match))
					return false;
			}

			_strings.RemoveAt(0);

			if (match.StartsWith("$PQTMVERNO"))
				CheckForVersion(str);

			if (!_strings.Any())
				LogX("GPS Startup Commands Complete");

			SendTopCommand();
			return true;
		}

		public void StartInitialiseProcess()
		{
			LogX("GPS Queue StartInitialiseProcess");
			_strings.Clear();

			_strings.Add("PQTMVERNO");
			_strings.Add("PQTMCFGSVIN,W,1,43200,0,0,0,0");
			_strings.Add("PAIR432,1");
			_strings.Add("PAIR434,1");
			_strings.Add("PAIR436,1");

			SendTopCommand();
		}

		public void CheckForTimeouts()
		{
			if (!_strings.Any())
				return;

			if ((Environment.TickCount - _timeSent) > 8000)
			{
				LogX($"E940 - Timeout on {_strings.First()}");
				SendTopCommand();
			}
		}

		private void SendTopCommand()
		{
			if (!_strings.Any())
				return;
			SendCommand(_strings.First());
			_timeSent = Environment.TickCount;
		}

		//////////////////////////////////////////////////////////////////////////
		// Send a command to the GPS unit
		//	Builds 
		//		PQTMVERNO
		// into
		//		$PQTMVERNO*58\r\n
		// $PAIR432,1*22
		private void SendCommand(string command)
		{
			LogX($"GPS -> {command}");

			// Make checksum
			byte checksum = CalculateChecksum(command);

			// Final string starting with $ + command + * + checksum in hex + \r\n
			string finalCommand = $"${command}*{checksum:X2}\r\n";

			try
			{
				_port.Write(finalCommand);
			}
			catch (Exception ex)
			{
				LogX("Error writing to serial port: " + ex.Message);
				_port.Close();
				Environment.Exit(1);
			}
		}

		private byte CalculateChecksum(string data)
		{
			byte checksum = 0;
			foreach (char c in data)
			{
				checksum ^= (byte)c;
			}
			return checksum;
		}

		private void LogX(string message)
		{
			Log.Ln(message);
		}
	}
}
