using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

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
			suppressionsInfo.SkippedFilesMask = FilesLines.Items;
			suppressionsInfo.SkippedIncludesMask = IncludesLines.Items;

			suppressionsInfo.SaveToFile(suppressionsFilePath);

			Close();
		}

		private void CancelClick(object sender, RoutedEventArgs e)
		{
			Close();
		}
	}
}
