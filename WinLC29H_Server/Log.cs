using System;
using System.IO;

namespace WinLC29H_Server
{
	static class Log
	{
		static string LogFileName { get; } = $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

		static object _lock = new object();

		/// <summary>
		/// Log the results to the console and to the log file
		/// </summary>
		internal static void Write(string data)
		{
			Console.Write(data);
			lock (_lock)
			{
				File.AppendAllText(LogFileName, data);
			}
		}
		internal static void Ln(string data)
		{
			Write(DateTime.Now.ToString("HH:mm:ss.fff") + " > "+ data + Environment.NewLine);
		}

	}
}
