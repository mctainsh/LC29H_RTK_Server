using System;
using System.IO.Ports;
using WinRtkHost.Models;
using WinRtkHost.Models.GPS;
using WinRtkHost.Properties;

namespace WinRtkHost
{
	class Program
	{
		static GpsParser _gpsParser;

		internal static bool IsLC29H => Settings.Default.GPSReceiverType == "LC29H";
		internal static bool IsUM980 => Settings.Default.GPSReceiverType == "UM980";
		internal static bool IsUM982 => Settings.Default.GPSReceiverType == "UM982";

		static void Main(string[] args)
		{
			try
			{
				Log.Ln($"Starting receiver {Settings.Default.GPSReceiverType}\r\n" +
					$"\t LC29H : {IsLC29H}\r\n" +
					$"\t UM980 : {IsUM980}\r\n" +
					$"\t UM982 : {IsUM982}");

				if (!IsLC29H && !IsUM980 && !IsUM982)
				{
					Log.Ln("Unknown receiver type");
					return;
				}

				// List serial ports
				var ports = SerialPort.GetPortNames();
				Log.Ln("Available Ports:");
				foreach (var n in ports)
					Log.Ln("\t" + n);

				// Open serial port
				var portName = Settings.Default.ComPort;
				var port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One);
				//				_gnssParser = new GNSSParser(port);

				port.Open();
				Log.Ln($"Port {portName} opened");

				_gpsParser = new GpsParser(port);

				// Open a continous client socket connection to NTRIP caster
				//				Task.Run(() => ConnectToCaster());

				// Slow status timer
				var lastStatus = DateTime.Now;

				// Loop until 'q' key is pressed
				while (true)
				{
					// Q to exit
					if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
						break;

					System.Threading.Thread.Sleep(100);

					// Check for timeouts
					_gpsParser.CheckTimeouts();

					// TODO : Every minute display the current stats
					if ((DateTime.Now - lastStatus).TotalSeconds > 60)
					{
						lastStatus = DateTime.Now;
						LogStatus(_gpsParser);
					}

					// Do nothing, just loop
				}
				Log.Ln("EXIT Via key press!");
			}
			catch (Exception ex)
			{
				Log.Ln(ex.ToString());
			}
		}

		/// <summary>
		/// Log the current system status
		/// </summary>
		private static void LogStatus(GpsParser gpsParser)
		{
			Log.Note($"=============================================");
			foreach (var s in gpsParser.NtripCasters)
				Log.Note(s.ToString());
			Log.Note(_gpsParser.ToString());
		}
	}
}
