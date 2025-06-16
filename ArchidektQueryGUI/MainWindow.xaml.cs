using Microsoft.Win32;
using QueryLibrary;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace ArchidektQueryGUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		bool _isQueryRunning;
		CancellationTokenSource? _cancelTokenSource;

		readonly ArchidektQueryTool _queryTool;

		public MainWindow()
		{
			InitializeComponent();
			
			//outputFileTypes.ItemsSource = typeof(Config.OutputMode).GetProperties();
			outputFileTypes.ItemsSource = Enum.GetValues(typeof(Config.OutputMode)).Cast<Config.OutputMode>();
			_queryTool = new ArchidektQueryTool();

			OnQueryProgress(QueryProgress.NotStarted);
		}

		private void usernameImportButton_Click(object sender, RoutedEventArgs e)
		{
			string? usernameList = GetTextFromTextFile("Open username file");
			if (usernameList != null)
			{
				usernameBox.Text = usernameList;
			}
		}

		private void cardsImportButton_Click(object sender, RoutedEventArgs e)
		{
			string? cardList = GetTextFromTextFile("Open card list file");
			if (cardList != null)
			{
				cardsBox.Text = cardList;
			}
		}

		private string? GetTextFromTextFile(string dialogTitle)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Filter = "txt files (*.txt)|*.txt";
			openFileDialog.Title = dialogTitle;
			openFileDialog.CheckFileExists = true;
			openFileDialog.CheckPathExists = true;
			openFileDialog.InitialDirectory = Directory.GetCurrentDirectory();

			bool? showDialog = openFileDialog.ShowDialog();
			if (showDialog != null && showDialog.Value)
			{
				//Get the path of specified file
				string filePath = openFileDialog.FileName;

				//Read the contents of the file into a stream
				var fileStream = openFileDialog.OpenFile();

				using (StreamReader reader = new StreamReader(fileStream))
				{
					return reader.ReadToEnd();
				}
			}

			return null;
		}

		// TODO: Figure out how to show failed exceptions? - Maybe a pop up lol
		private async void runQueryButton_Click(object sender, RoutedEventArgs e)
		{
			runQueryButton.IsEnabled = false;
			cancelQueryButton.IsEnabled = true;
			if (_isQueryRunning) return;
			_isQueryRunning = true;

			Config config = new Config()
			{
				AllowPartialMatches = allowPartialMatchesBox.IsChecked.HasValue ? allowPartialMatchesBox.IsChecked.Value : false,
				IncludeDeckInfo = includeUserDeckInfoBox.IsChecked.HasValue ? includeUserDeckInfoBox.IsChecked.Value : true,
				LogToFile = createLogFileBox.IsChecked.HasValue ? createLogFileBox.IsChecked.Value : true,
				OpenOutputAutomatically = openOutputAutomaticallyBox.IsChecked.HasValue ? openOutputAutomaticallyBox.IsChecked.Value : true,
			};

			_queryTool.SetNewConfig(config);
			_cancelTokenSource = new CancellationTokenSource();
			
			try
			{
				await _queryTool.Run(usernameBox.Text, cardsBox.Text, OnQueryProgress);
			}
			catch (Exception ex)
			{

			}
		}

		private void cancelQueryButton_Click(object sender, RoutedEventArgs e)
		{
			OnQueryProgress(QueryProgress.Canceled);
		}

		private void OnQueryProgress(QueryProgress progress)
		{
			switch (progress)
			{
				case QueryProgress.NotStarted:
					queryProgressBar.Value = 0;
					queryProgressBar.IsIndeterminate = false;
					queryProgressBarText.Text = "Query not yet started.";
					break;
				case QueryProgress.GatheringQueryInfo:
					queryProgressBar.Value = 25;
					queryProgressBar.IsIndeterminate = false;
					queryProgressBarText.Text = "Gathering Query info.";
					break;
				case QueryProgress.StartingQuery:
					queryProgressBar.Value = 50;
					queryProgressBar.IsIndeterminate = true;
					queryProgressBarText.Text = "Starting Query...";
					break;
				case QueryProgress.CreatingOutput:
					queryProgressBar.Value = 75;
					queryProgressBar.IsIndeterminate = false;
					queryProgressBarText.Text = "Creating output.";
					break;
				case QueryProgress.Done:
					queryProgressBar.Value = 100;
					queryProgressBar.IsIndeterminate = false;
					queryProgressBarText.Text = "Query finished.";
					_isQueryRunning = false;
					runQueryButton.IsEnabled = true;
					cancelQueryButton.IsEnabled = false;
					break;
				case QueryProgress.Canceled:
					queryProgressBar.Value = 0;
					queryProgressBar.IsIndeterminate = false;
					queryProgressBarText.Text = "Query canceled.";
					_isQueryRunning = false;
					runQueryButton.IsEnabled = true;
					cancelQueryButton.IsEnabled = false;
					_cancelTokenSource?.Cancel();
					break;
				default:
					break;
			}
		}
	}
}