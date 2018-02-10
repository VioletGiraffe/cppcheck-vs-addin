using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSPackage.CPPCheckPlugin
{
	public class SuppressionsInfo
	{
		// list of suppressions in cppcheck format
		public HashSet<string> SuppressionLines = new HashSet<string>();

		// list of regular expressions with files that should be excluded from check
		public HashSet<string> SkippedFilesMask = new HashSet<string>();

		// list of regular expressions with include paths that should be excluded from includes list
		public HashSet<string> SkippedIncludesMask = new HashSet<string>();

		public void SaveToFile(string suppressionsFilePath)
		{
			HashSet<string> lines = new HashSet<string>();
			lines.Add("[cppcheck]");
			lines.UnionWith(SuppressionLines);
			lines.Add("[cppcheck_files]");
			lines.UnionWith(SkippedFilesMask);
			lines.Add("[cppcheck_includes]");
			lines.UnionWith(SkippedIncludesMask);

			System.IO.FileInfo file = new System.IO.FileInfo(suppressionsFilePath);
			file.Directory.Create(); // If the directory already exists, this method does nothing.
			File.WriteAllLines(suppressionsFilePath, lines);
		}

		public void LoadFromFile(string suppressionsFilePath)
		{
			SuppressionLines.Clear();
			SkippedFilesMask.Clear();
			SkippedIncludesMask.Clear();

			if (File.Exists(suppressionsFilePath))
			{
				using (StreamReader stream = File.OpenText(suppressionsFilePath))
				{
					string currentGroup = "";
					var line = stream.ReadLine();
					while (line != null)
					{
						if (line.StartsWith("["))
						{
							currentGroup = line.Replace("[", "").Replace("]", "");
						}
						else
						{
							if (currentGroup == "cppcheck")
							{
								AddSuppressionLine(line);
							}
							else if (currentGroup == "cppcheck_files")
							{
								SkippedFilesMask.Add(line);
							}
							else if (currentGroup == "cppcheck_includes")
							{
								SkippedIncludesMask.Add(line);
							}
						}

						line = stream.ReadLine();
					}
				}
			}
		}

		public void AddSuppressionLine(string line)
		{
			var components = line.Split(':');
			if (components.Length >= 2 && !components[1].StartsWith("*")) // id and some path without "*"
				components[1] = "*" + components[1]; // adding * in front

			string suppression = components[0];
			if (components.Length > 1)
				suppression += ":" + components[1];
			if (components.Length > 2)
				suppression += ":" + components[2];

			if (!string.IsNullOrEmpty(suppression))
				SuppressionLines.Add(suppression.Replace("\\\\", "\\"));
		}

		public void UnionWith(SuppressionsInfo suppressionsInfo)
		{
			SuppressionLines.UnionWith(suppressionsInfo.SuppressionLines);
			SkippedFilesMask.UnionWith(suppressionsInfo.SkippedFilesMask);
			SkippedIncludesMask.UnionWith(suppressionsInfo.SkippedIncludesMask);
		}
	}
}
