using Microsoft.Win32;
using QueryLibrary;
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
		public MainWindow()
		{
			InitializeComponent();
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

		private void runQueryButton_Click(object sender, RoutedEventArgs e)
		{
			List<string> usernames = usernameBox.Text.Split('\n').Where(u => !(string.IsNullOrWhiteSpace(u) || string.IsNullOrEmpty(u))).ToList();
			List<string> cards = cardsBox.Text.Split('\n').Where(u => !(string.IsNullOrWhiteSpace(u) || string.IsNullOrEmpty(u))).ToList();

			Config config = new Config()
			{
				AllowPartialMatches = allowPartialMatchesBox.IsChecked.HasValue ? allowPartialMatchesBox.IsChecked.Value : false,
				IncludeDeckInfo = includeUserDeckInfoBox.IsChecked.HasValue ? includeUserDeckInfoBox.IsChecked.Value : true,
				LogToFile = createLogFileBox.IsChecked.HasValue ? createLogFileBox.IsChecked.Value : true,
				OpenOutputAutomatically = openOutputAutomaticallyBox.IsChecked.HasValue ? openOutputAutomaticallyBox.IsChecked.Value : true,
			};
		}
	}
}