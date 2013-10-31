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
		public CppcheckSettings()
		{
			InitializeComponent();
			Activated += onActivated;
			Closed += OnClosed;

			ArgumentsEditor.MaxLines = 1;
			ArgumentsEditor.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
		}

		private void onActivated(object o, EventArgs e)
		{
			InconclusiveChecks.IsChecked = Properties.Settings.Default.InconclusiveChecksEnabled;
			ArgumentsEditor.Text = Properties.Settings.Default.DefaultArguments;
		}

		private void OnClosed(object o, EventArgs e)
		{
			Properties.Settings.Default["DefaultArguments"] = String.IsNullOrEmpty(ArgumentsEditor.Text) ? DefaultArguments.Replace('\n', ' ').Replace('\r', ' ') : ArgumentsEditor.Text;
		}

		private void inconclusive_Unchecked(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["InconclusiveChecksEnabled"] = false;
		}

		private void inconclusive_Checked(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["InconclusiveChecksEnabled"] = true;
		}
		private void onDefaultArguments(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default["DefaultArguments"] = DefaultArguments;
			ArgumentsEditor.Text = DefaultArguments;
		}

		public static string DefaultArguments = "--enable=style,information,warning,performance,portability --inline-suppr -q --force --template=vs";
	}
}
