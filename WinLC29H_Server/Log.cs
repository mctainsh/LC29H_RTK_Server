using System;
using System.IO;

namespace WinLC29H_Server
{
	static class Log
	{
		static internal string LogFileName { private set; get; }
		static DateTime _day;
		static object _lock = new object();

		/// <summary>
		/// Log the results to the console and to the log file
		/// </summary>
		internal static void Write(string data)
		{
			Console.Write(data);
			lock (_lock)
			{
				try
				{
					File.AppendAllText(LogFileName, data);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error writing to log file: " + ex);
				}
			}
		}
		internal static void Ln(string data)
		{
			var now = DateTime.Now;
			if (now.Day != _day.Day)
			{
				_day = now;
				LogFileName = $"Log_{now:yyyyMMdd_HHmmss}.txt";
			}
			Write(now.ToString("HH:mm:ss.fff") + " > "+ data + Environment.NewLine);
		}
	}
}
