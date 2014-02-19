using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Xml;

namespace VSPackage.CPPCheckPlugin
{
	class ChecksPanel
	{
		private StackPanel mPanel;

		class CheckInfo
		{
			public string id;
			public string toolTip;
			public CheckBox box;
		};
		class SeverityInfo
		{
			public string id;
			public string toolTip;
			public List<CheckInfo> checks = new List<CheckInfo>();

			public CheckBox box;
			public ScrollViewer scrollView;
		};
		Dictionary<string, SeverityInfo> mChecks = new Dictionary<string, SeverityInfo>();

		public ChecksPanel(StackPanel panel)
		{
			mPanel = panel;

			BuildChecksList();
			GenerateControls();
			LoadSettings();
		}

		public void LoadSettings()
		{
			var enabledSeverities = Properties.Settings.Default.SeveritiesString.Split(',');
			HashSet<string> suppressions = new HashSet<string>(Properties.Settings.Default.SuppressionsString.Split(','));
			
			foreach (var severity in mChecks)
			{
				severity.Value.scrollView.IsEnabled = false;
				foreach (CheckInfo check in severity.Value.checks)
				{
					check.box.IsChecked = suppressions.Contains(check.id) == false;
				}
			}

			mChecks["error"].box.IsChecked = true;
			mChecks["error"].box.IsEnabled = false;
			mChecks["error"].box.Content = "error (can't be disabled)";
			mChecks["error"].scrollView.IsEnabled = true;
			foreach (var severity in enabledSeverities)
			{
				if (mChecks.ContainsKey(severity))
				{
					mChecks[severity].box.IsChecked = true;
					mChecks[severity].scrollView.IsEnabled = true;
				}
			}
		}

		private string GetSeveritiesString()
		{
			string result = "";
			foreach (var severity in mChecks)
			{
				if (severity.Key != "error" && severity.Value.box.IsChecked == true)
				{
					if (result.Length != 0)
						result += ",";
					result += severity.Value.id;
				}
			}
			return result;
		}

		private string GetSuppressionsString()
		{
			string result = "";
			foreach (var severity in mChecks)
			{
				foreach (CheckInfo check in severity.Value.checks)
				{
					if (check.box.IsChecked == false)
					{
						if (result.Length != 0)
							result += ",";
						result += check.id;
					}
				}
			}
			return result;
		}

		private void BuildChecksList()
		{
			var checksList = LoadChecksList();

			foreach (XmlNode node in checksList.SelectNodes("//errors/error"))
			{
				string id = node.Attributes["id"].Value;
				string severity = node.Attributes["severity"].Value;
				string message = node.Attributes["msg"].Value;
				string verboseMessage = node.Attributes["verbose"].Value;

				if (!mChecks.ContainsKey(severity))
					mChecks.Add(severity, new SeverityInfo { id = severity, toolTip = "TODO" });

				mChecks[severity].checks.Add(new CheckInfo { id = id, toolTip = verboseMessage });
			}
		}

		private XmlDocument LoadChecksList()
		{
			using (var process = new System.Diagnostics.Process())
			{
				var startInfo = process.StartInfo;
				startInfo.UseShellExecute = false;
				startInfo.CreateNoWindow = true;
				startInfo.RedirectStandardOutput = true;
				startInfo.FileName = Properties.Settings.Default.CPPcheckPath;
				startInfo.Arguments = "--errorlist --xml-version=2";
				process.Start();
				String output;
				using (var outputStream = process.StandardOutput)
				{
					output = outputStream.ReadToEnd();
				}
				process.WaitForExit();

				var checksList = new XmlDocument();
				checksList.LoadXml(output);
				return checksList;
			}
		}

		private void GenerateControls()
		{
			foreach (var severity in mChecks)
			{
				var severityCheckBox = new CheckBox();
				severity.Value.box = severityCheckBox;
				severityCheckBox.Name = severity.Value.id;
				severityCheckBox.Content = severity.Value.id;
				severityCheckBox.ToolTip = severity.Value.toolTip;

				severityCheckBox.Checked += Severity_Changed;
				severityCheckBox.Unchecked += Severity_Changed;

				mPanel.Children.Add(severityCheckBox);

				var scrollView = new ScrollViewer();
				scrollView.Margin = new System.Windows.Thickness(20, 0, 0, 0);
				scrollView.MaxHeight = 100;
				mPanel.Children.Add(scrollView);

				var subPanel = new StackPanel();
				severity.Value.scrollView = scrollView;

				scrollView.Content = subPanel;

				foreach (CheckInfo check in severity.Value.checks)
				{
					var box = new CheckBox();
					check.box = box;
					box.Name = check.id;
					box.Content = check.id;
					box.ToolTip = check.toolTip;

					box.Checked += Check_Changed;
					box.Unchecked += Check_Changed;

					subPanel.Children.Add(box);
				}
			}
		}

		private void Severity_Changed(object sender, System.Windows.RoutedEventArgs e)
		{
			var box = sender as CheckBox;
			if (mChecks.ContainsKey(box.Name))
			{
				mChecks[box.Name].scrollView.IsEnabled = box.IsChecked == true;
			}
			Properties.Settings.Default.SeveritiesString = GetSeveritiesString();
			Properties.Settings.Default.Save();
		}

		private void Check_Changed(object sender, System.Windows.RoutedEventArgs e)
		{
			Properties.Settings.Default.SuppressionsString = GetSuppressionsString();
			Properties.Settings.Default.Save();
		}
	}
}
