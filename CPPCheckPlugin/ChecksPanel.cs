using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Xml;

namespace VSPackage.CPPCheckPlugin
{
	class ChecksPanel
	{
		private StackPanel mPanel;

		// copypasted from cppcheck documentation
		private Dictionary<string, string> SeverityToolTips = new Dictionary<string, string>()
		{
			{"error", "Programming error.\nThis indicates severe error like memory leak etc.\nThe error is certain."},
			{"warning", "Used for dangerous coding style that can cause severe runtime errors.\nFor example: forgetting to initialize a member variable in a constructor."},
			{"style", "Style warning.\nUsed for general code cleanup recommendations. Fixing these will not fix any bugs but will make the code easier to maintain.\nFor example: redundant code, unreachable code, etc."},
			{"performance", "Performance warning.\nNot an error as is but suboptimal code and fixing it probably leads to faster performance of the compiled code."},
			{"portability", "Portability warning.\nThis warning indicates the code is not properly portable for different platforms and bitnesses (32/64 bit). If the code is meant to compile in different platforms and bitnesses these warnings should be fixed."},
			{"information", "Checking information.\nInformation message about the checking (process) itself. These messages inform about header files not found etc issues that are not errors in the code but something user needs to know."},
			{"debug", "Debug message.\nDebug-mode message useful for the developers."}
		};

		class CheckInfo
		{
			public string id;
			public string label;
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
					mChecks.Add(severity, new SeverityInfo { id = severity, toolTip = SeverityToolTips[severity] });

				string checkToolTip = FormatTooltip(id, severity, message, verboseMessage);

				mChecks[severity].checks.Add(new CheckInfo { id = id, toolTip = checkToolTip, label = message });
			}
		}

		private static string FormatTooltip(string id, string severity, string message, string verboseMessage)
		{
			string multilineToolTip = "";
			string remainingToolTip = "id : " + id + "\n" + verboseMessage;
			while (remainingToolTip.Length > 100)
			{
				int spaceIdx = remainingToolTip.IndexOf(' ', 100);
				if (spaceIdx == -1)
					break;
				multilineToolTip += remainingToolTip.Substring(0, spaceIdx) + Environment.NewLine;
				remainingToolTip = remainingToolTip.Substring(spaceIdx + 1);
			}
			multilineToolTip += remainingToolTip;
			return multilineToolTip;
		}

		private XmlDocument LoadChecksList()
		{
			using (var process = new System.Diagnostics.Process())
			{
				var startInfo = process.StartInfo;
				startInfo.UseShellExecute = false;
				startInfo.CreateNoWindow = true;
				startInfo.RedirectStandardOutput = true;
				startInfo.WorkingDirectory = Path.GetDirectoryName(Properties.Settings.Default.CPPcheckPath);
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

				severity.Value.checks.Sort((check1, check2) => check1.label.CompareTo(check2.label));
				foreach (CheckInfo check in severity.Value.checks)
				{
					var box = new CheckBox();
					check.box = box;
					box.Name = check.id;
					box.Content = /*check.id + ":\t" +*/ check.label;
					box.ToolTip = check.toolTip;

					box.Checked += Check_Changed;
					box.Unchecked += Check_Changed;

					subPanel.Children.Add(box);
				}
			}
		}

		private void Severity_Changed(object sender, System.Windows.RoutedEventArgs e)
		{
			var box = (CheckBox)sender;
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
