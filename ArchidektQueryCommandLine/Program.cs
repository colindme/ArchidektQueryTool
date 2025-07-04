﻿using System.Collections.Concurrent;
using System.Text;
using QueryLibrary;

namespace ArchidektQueryCommandLine
{
	class Program
	{
		static readonly Config _config = new Config();
		static string _cardFileName = "";
		static string _usernameFileName = "";

		public static void Main(string[] args)
		{
			try
			{
				// Initialize (load command line arguments & initialize logger)
				if (ParseCommandLineArgs(args))
				{
					ArchidektQueryTool queryTool = new ArchidektQueryTool(_config);
					string fullCardsText = File.ReadAllText(_cardFileName);
					string fullUsernameText = File.ReadAllText(_usernameFileName);
					queryTool.Run(fullUsernameText, fullCardsText, OnProgressReport);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ERROR: Encountered unhandled exception: {ex.Message}");
			}
		}

		private static bool ParseCommandLineArgs(string[] args)
		{
			bool cardFileSet = false, usernameFileSet = false;

			if (args.Length == 0)
			{
				Console.WriteLine($"ERROR: Required command line arguments not passed in. Please use -Help, /?, or -h to get more information.");
			}

			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "-Help" || args[i] == "/?" || args[i] == "-h")
				{
					Console.WriteLine(GetHelpString());
					return false;
				}

				if (!args[i].StartsWith('-'))
				{
					Console.WriteLine($"Unknown flag {args[i]} | Make sure command line flags are prepended with a -");
					continue;
				}
				// Every other command line flag requires a followup argument so if we are at the end of the array, we can't discern anymore info 
				if (i == args.Length - 1) continue;

				string argInput = args[i + 1];
				try
				{
					switch (args[i])
					{
						case "-CardListFilePath":
							if (IsStringValidPath(argInput))
							{
								_cardFileName = argInput;
								cardFileSet = true;
								i++;
							}
							else
							{
								Console.WriteLine("ERROR: Failed to load card list because passed in argument had invalid characters");
								return false;
							}
							break;
						case "-UsernameListFilePath":
							if (IsStringValidPath(argInput))
							{
								_usernameFileName = argInput;
								usernameFileSet = true;
								i++;
							}
							else
							{
								Console.WriteLine("ERROR: Failed to load Archidekt username list because passed in argument had invalid characters");
								return false;
							}
							break;
						case "-AllowPartialMatches":
							_config.AllowPartialMatches = bool.Parse(argInput);
							i++;
							break;
						case "-IncludeDeckInfo":
							_config.IncludeDeckInfo = bool.Parse(argInput);
							i++;
							break;
						case "-LogFilePath":
							if (IsStringValidPath(argInput))
							{
								_config.LogFilePath = argInput;
							}
							else
							{
								Console.WriteLine("ERROR: Passed in -LogFilePath argument had invalid characters");
							}
							i++;
							break;
						case "-LogToConsole":
							_config.LogToConsole = bool.Parse(argInput);
							i++;
							break;
						case "-LogToFile":
							_config.LogToFile = bool.Parse(argInput);
							i++;
							break;
						case "-MaxRetries":
							_config.MaxRetries = int.Parse(argInput);
							i++;
							break;
						case "-OpenOutputAutomatically":
							_config.OpenOutputAutomatically = bool.Parse(argInput);
							i++;
							break;
						case "-OutputFilePath":
							if (IsStringValidPath(argInput))
							{
								_config.OutputFilePath = argInput;
							}
							else
							{
								Console.WriteLine("ERROR: Passed in -OutputFilePath argument had invalid characters");
							}
							i++;
							break;
						case "-OutputFileType":
							if (Enum.TryParse(argInput, out Config.OutputMode result))
							{
								_config.OutputFileType = result;
							}
							else
							{
								Console.WriteLine("ERROR: Failed to parse passed in value to OutputMode. Valid options are TextFile or HtmlFile. Defaulting to TextFile");
							}
							i++;
							break;
						case "-OutputToConsole":
							_config.OutputToConsole = bool.Parse(argInput);
							i++;
							break;
						case "-OutputToFile":
							_config.OutputToFile = bool.Parse(argInput);
							i++;
							break;
						default:
							Console.WriteLine("WARNING: Unknown flag passed in, skipping...");
							break;
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"ERROR: Encountered exception while attempting to parse command line argument {args[i]} with value {argInput} | Exception: {ex}");
				}
			}

			return cardFileSet && usernameFileSet;
		}

		private static bool IsStringValidPath(string s)
		{
			char[] invalidChars = Path.GetInvalidPathChars();
			foreach (char c in s)
			{
				if (invalidChars.Contains(c)) return false;
			}
			return true;
		}

		private static void OnProgressReport(QueryProgress progress)
		{
			switch (progress)
			{
				case QueryProgress.GatheringQueryInfo:
					Console.WriteLine("Gathering query info");
					break;
				case QueryProgress.StartingQuery:
                    Console.WriteLine("Starting Archidekt query");
                    break;
				case QueryProgress.CreatingOutput:
                    Console.WriteLine("Creating query from results");
                    break;
				case QueryProgress.Done:
                    Console.WriteLine("Finished query");
                    break;
				default:
					break;
			}
		}

		private static string GetHelpString()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("Archidekt Query Command Line Tool Help");
			sb.AppendLine("*************");
			// -AllowPartialMatches
			sb.AppendLine("\n-AllowPartialMatches");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: False");
			sb.AppendLine("Accepts: True/False");
			sb.AppendLine("Description: If set to True, partial card name matches will be returned (e.g. Snap in the query list could return Snapping Voidcraw). Most useful if using a list not copied directly from Archidekt.");

			// -IncludeDeckInfo
			sb.AppendLine("\n-IncludeDeckInfo");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: True");
			sb.AppendLine("Accepts: True/False");
			sb.AppendLine("Description: If set to True, the decks for each user in the UsernameList will be queried for any collection matches found. This can be helpful to find any cards already in use from a collection.");
			// -LogFilePath
			sb.AppendLine("\n-LogFilePath");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: ./log.txt");
			sb.AppendLine("Accepts: string path");
			sb.AppendLine("Description: The path and file name for the log file. Will overwrite the current file contents if a file with the same name is already present. Ignored if -LogToFile is set to False");

			// -LogToConsole
			sb.AppendLine("\n-LogToConsole");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: True");
			sb.AppendLine("Accepts: True/False");
			sb.AppendLine("Description: Whether the logger should log to the console.");

			// -LogToFile
			sb.AppendLine("\n-LogToFile");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: True");
			sb.AppendLine("Accepts: True/False");
			sb.AppendLine("Description: Whether the logger should log to a file. If set to False, -LogFilePath will be ignored");

			// -MaxRetries
			sb.AppendLine("\n-MaxRetries");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: 5");
			sb.AppendLine("Accepts: Int");
			sb.AppendLine("Description: The number of retries for queries in the case of intermittent HTTP errors. If set to 0, queries will be retried until cancelled.");

			// -OpenOutputAutomatically
			sb.AppendLine("\n-OpenOutputAutomatically");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: True");
			sb.AppendLine("Accepts: True/False");
			sb.AppendLine("Description: If set to True, the output file will automatically be opened upon completion of querys. Ignored if -OutputToFile is set to False.");

			// -OutputFilePath
			sb.AppendLine("\n-OutputFilePath");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: ./output.txt");
			sb.AppendLine("Accepts: string path");
			sb.AppendLine("Description: The path and file name for the output file. Will overwrite the current file contents if a file with the same name is already present. Ignored if -OutputToFile is set to False.");

			// -OutputFileType
			sb.AppendLine("\n-OutputFileType");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: TextFile");
			sb.AppendLine("Accepts: TextFile or HtmlFile");
			sb.AppendLine("Description: Determines the format for the Output file. TextFile is more minimal while HtmlFile includes some additional formatting as well as image links");

			// -OutputToConsole
			sb.AppendLine("\n-OutputToConsole");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: True");
			sb.AppendLine("Accepts: True/False");
			sb.AppendLine("Description: If set to True, the output will be printed to the Console after the query is done querying.");

			// -OutputToFile
			sb.AppendLine("\n-OutputToFile");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: True");
			sb.AppendLine("Accepts: True/False");
			sb.AppendLine("Description: If set to True, the output will be saved to a file after the query is done querying.");

			// -Help / -? / -h
			sb.AppendLine("\n-Help | -? | -h");
			sb.AppendLine("Required: No");
			sb.AppendLine("Default: N/A");
			sb.AppendLine("Accepts: N/A");
			sb.AppendLine("Description: If set to True, the output file will automatically be opened upon completion of querys. Ignored if -OutputToFile is set to False.");

			// -CardListFilePath
			sb.AppendLine("\n-CardListFilePath");
			sb.AppendLine("Required: Yes");
			sb.AppendLine("Default: N/A");
			sb.AppendLine("Accepts: string path");
			sb.AppendLine("Description: The path of the file which contains the list of cards to be queried. Each entry is expected to be new-line separated");

			// -UsernameListFilePath
			sb.AppendLine("\n-UsernameListFilePath");
			sb.AppendLine("Required: Yes");
			sb.AppendLine("Default: N/A");
			sb.AppendLine("Accepts: string path");
			sb.AppendLine("Description: The path of the file which contains the list of Archidekt usernames to be queried. Each entry is expected to be new-line separated");

			return sb.ToString();
		}
	}
}
