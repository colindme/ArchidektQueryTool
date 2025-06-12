using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QueryLibrary
{
	internal class Logger : IDisposable
	{
		private bool _logToConsole;
		private bool _logToFile;
		private StreamWriter? _logFile;

		public Logger(bool logToConsole, bool logToFile, string logFilePath = "./", string logFileName = "log.txt")
		{
			_logToConsole = logToConsole;
			_logToFile = logToFile;
			try
			{
				if (logToFile)
				{
					if (logFilePath.Contains('/') && !logFilePath.EndsWith('/'))
					{
						logFilePath += '/';
					}
					else if (logFilePath.Contains('\\') && !logFilePath.EndsWith('\\'))
					{
						logFilePath += '\\';
					}

					_logFile = new StreamWriter(logFilePath + logFileName);
				}
			}
			catch (Exception ex)
			{
				if (_logToConsole)
				{
					Console.WriteLine($"Failed to create log file due to exception: {ex.Message}");
				}
			}
		}

		public void Log(string log)
		{
			if (_logToConsole)
			{
				Console.WriteLine(log);
			}

			if (_logToFile && _logFile != null)
			{
				lock (_logFile)
				{
					_logFile.WriteLine(log);
				}
			}
		}

		void IDisposable.Dispose()
		{
			if (_logFile != null)
			{
				_logFile.Dispose();
				_logFile.Close();
			}
		}
	}
}
