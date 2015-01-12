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

namespace VSPackage.CPPCheckPlugin.SuppressionSettingsUI
{
	/// <summary>
	/// Interaction logic for SuppressionsSettings.xaml
	/// </summary>
	public partial class SuppressionsSettings : Window
	{
		public string suppressionsFilePath;

		public SuppressionsSettings(ICodeAnalyzer.SuppressionStorage suppressionStorage, string projectBasePath = null, string projectName = null)
		{
			InitializeComponent();

			suppressionsFilePath = ICodeAnalyzer.suppressionsFilePathByStorage(suppressionStorage, projectBasePath, projectName);
			SuppressionsInfo suppressionsInfo = new SuppressionsInfo();
			suppressionsInfo.LoadFromFile(suppressionsFilePath);

			CppcheckLines.Items = suppressionsInfo.SuppressionLines;
			FilesLines.Items = suppressionsInfo.SkippedFilesMask;
			IncludesLines.Items = suppressionsInfo.SkippedIncludesMask;
		}

		private void SaveClick(object sender, RoutedEventArgs e)
		{
			SuppressionsInfo suppressionsInfo = new SuppressionsInfo();

			suppressionsInfo.SuppressionLines = CppcheckLines.Items;
			while (suppressionsInfo.SuppressionLines.Remove("")) { /* nothing */ }
			suppressionsInfo.SkippedFilesMask = FilesLines.Items;
			while (suppressionsInfo.SkippedFilesMask.Remove("")) { /* nothing */ }
			suppressionsInfo.SkippedIncludesMask = IncludesLines.Items;
			while (suppressionsInfo.SkippedIncludesMask.Remove("")) { /* nothing */ }

			suppressionsInfo.SaveToFile(suppressionsFilePath);

			Close();
		}

		private void CancelClick(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
