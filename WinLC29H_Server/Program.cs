using System;
using System.IO.Ports;

namespace WinLC29H_Server
{
	class Program
	{
		static GpsParser _gpsParser;

		static void Main(string[] args)
		{
			try
			{
				Log.Ln("Starting receiver");

				// List serial ports
				var ports = SerialPort.GetPortNames();
				Log.Ln("Available Ports:");
				foreach (var n in ports)
					Log.Ln("\t" + n);

				// Open serial port
				var portName = ports[0];
				var port = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One);
				//				_gnssParser = new GNSSParser(port);

				port.Open();
				Log.Ln($"Port {portName} opened");

				_gpsParser = new GpsParser(port);

				// Open a continous client socket connection to NTRIP caster
				//				Task.Run(() => ConnectToCaster());





				// Wait for user input
				Console.WriteLine("Press any key to exit");
				Console.ReadKey();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error: " + ex.Message);
			}
		}


	}
}
