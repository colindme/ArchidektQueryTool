using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QueryLibrary
{
	public class Config
	{
		public bool AllowPartialMatches { get; set; } = false;
		public bool IncludeDeckInfo { get; set; } = true;
		public string LogFilePath { get; set; } = "./log.txt";
		public bool LogToConsole { get; set; } = true;
		public bool LogToFile { get; set; } = true;
		public int MaxRetries { get; set; } = 5;
		public bool OpenOutputAutomatically { get; set; } = false;
		public string OutputFilePath { get; set; } = "./output.txt";
		public OutputMode OutputFileType { get; set; } = Config.OutputMode.TextFile;
		public bool OutputToConsole { get; set; } = true;
		public bool OutputToFile { get; set; } = true;

		public Config() { }

		public enum OutputMode
		{
			TextFile,
			HtmlFile
		}
	}
}
