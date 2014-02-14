using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VSPackage.CPPCheckPlugin
{
	/// <summary>
	/// Interaction logic for CppcheckSettings.xaml
	/// </summary>
	public partial class CppcheckSettings : Window
	{
		public static string DefaultArguments = "--inline-suppr -q --force --template=vs";
		private ChecksPanel mChecksPanel;
		public CppcheckSettings()
		{
			InitializeComponent();
			Activated += onActivated;
			Closed += OnClosed;

			ArgumentsEditor.MaxLines = 1;
			ArgumentsEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

			mChecksPanel = new ChecksPanel(Checks_Panel);
		}

		private void onActivated(object o, EventArgs e)
		{
			mChecksPanel.LoadSettings();
			InconclusiveChecks.IsChecked = Properties.Settings.Default.InconclusiveChecksEnabled;
			Project_OnlyCheckCurrentConfig.IsChecked = Properties.Settings.Default.ProjectOnlyCheckCurrentConfig;
			File_OnlyCheckCurrentConfig.IsChecked = Properties.Settings.Default.FileOnlyCheckCurrentConfig;
			ArgumentsEditor.Text = Properties.Settings.Default.DefaultArguments;
		}

		private void OnClosed(object o, EventArgs e)
		{
			Properties.Settings.Default["DefaultArguments"] = String.IsNullOrEmpty(ArgumentsEditor.Text) ? DefaultArguments.Replace('\n', ' ').Replace('\r', ' ') : ArgumentsEditor.Text;
			Properties.Settings.Default.Save();
		}

		private void inconclusive_Unchecked(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["InconclusiveChecksEnabled"] = false;
			Properties.Settings.Default.Save();
		}

		private void inconclusive_Checked(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["InconclusiveChecksEnabled"] = true;
			Properties.Settings.Default.Save();
		}
		private void onDefaultArguments(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["DefaultArguments"] = DefaultArguments;
			ArgumentsEditor.Text = DefaultArguments;
			Properties.Settings.Default.Save();
		}

		private void Project_OnlyCheckCurrentConfig_Checked(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["ProjectOnlyCheckCurrentConfig"] = true;
			Properties.Settings.Default.Save();
		}

		private void Project_OnlyCheckCurrentConfig_Unchecked(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["ProjectOnlyCheckCurrentConfig"] = false;
			Properties.Settings.Default.Save();
		}

		private void File_OnlyCheckCurrentConfig_Checked(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["FileOnlyCheckCurrentConfig"] = true;
			Properties.Settings.Default.Save();
		}

		private void File_OnlyCheckCurrentConfig_Unchecked(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["FileOnlyCheckCurrentConfig"] = true;
			Properties.Settings.Default.Save();
		}
	}
}
