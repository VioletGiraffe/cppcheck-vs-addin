using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VSPackage.CPPCheckPlugin
{
	public class SourceFile
	{
		public enum VCCompilerVersion { vc2003, vc2005, vc2008, vc2010, vc2012, vc2013, vc2015, vc2017, vc2019, vcFuture };

		public SourceFile(string fullPath, string projectBasePath, string projectName, string vcCompilerName)
		{
			_fullPath = cleanPath(fullPath);
			_projectBasePath = cleanPath(projectBasePath);
			_projectName = projectName;

			// Parsing the number
			String vcToolsNumberString = Regex.Match(vcCompilerName, @"\d+").Value;
			int vcToolsNumber = Int32.Parse(vcToolsNumberString);

			if (string.IsNullOrEmpty(vcCompilerName)) // Temporary workaround for #27
			{
				Debug.WriteLine("Couldn't extract VC tools name from project properties");
				_compilerVersion = VCCompilerVersion.vc2012;
			}
			else if (vcToolsNumber < 2003) // an even older version, still setting to vc2003 for now
				_compilerVersion = VCCompilerVersion.vc2003;
			else
			{
				switch (vcToolsNumber)
				{
					case 2003:
						_compilerVersion = VCCompilerVersion.vc2003;
						break;
					case 2005:
						_compilerVersion = VCCompilerVersion.vc2005;
						break;
					case 2008:
						_compilerVersion = VCCompilerVersion.vc2008;
						break;
					case 2010:
						_compilerVersion = VCCompilerVersion.vc2010;
						break;
					case 2012:
						_compilerVersion = VCCompilerVersion.vc2012;
						break;
					case 2013:
						_compilerVersion = VCCompilerVersion.vc2013;
						break;
					case 2015:
						_compilerVersion = VCCompilerVersion.vc2015;
						break;
					case 2017:
						_compilerVersion = VCCompilerVersion.vc2017;
						break;
					case 2019:
						_compilerVersion = VCCompilerVersion.vc2019;
						break;
					default:
						_compilerVersion = VCCompilerVersion.vcFuture;
						break;
				}
			}
		}

		// All include paths being added are resolved against projectBasePath
		public void addIncludePath(string path)
		{
			if (String.IsNullOrEmpty(_projectBasePath))
				return;
			else if (String.IsNullOrEmpty(path) || path.Equals(".") || path.Equals("\\\".\\\""))
				return;

			bool isAbsolutePath = false;
			try
			{
				isAbsolutePath = System.IO.Path.IsPathRooted(path);
			}
			catch (System.ArgumentException)
			{
				// seems like an invalid path, ignore
				return;
			}

			if (isAbsolutePath) // absolute path
				_includePaths.Add(cleanPath(path));
			else
			{
				// Relative path - converting to absolute
				String pathForCombine = path.Replace("\"", String.Empty).TrimStart('\\', '/');
				_includePaths.Add(cleanPath(Path.GetFullPath(Path.Combine(_projectBasePath, pathForCombine)))); // Workaround for Path.Combine bugs
			}
		}

		public void addIncludePaths(IEnumerable<string> paths)
		{
			foreach (string path in paths)
			{
				addIncludePath(path);
			}
		}

		public void addMacro(string macro)
		{
			if (!String.IsNullOrEmpty(macro))
            {
				_activeMacros.Add(macro);
			}
		}

		public void addMacros(IEnumerable<string> macros)
		{
			foreach (string macro in macros)
			{
				addMacro(macro);
			}
		}

		public void addMacroToUndefine(string macro)
		{
			if (!String.IsNullOrEmpty(macro))
			{
				_macrosToUndefine.Add(macro);
			}
		}

		public void addMacrosToUndefine(IEnumerable<string> macros)
		{
			foreach (string macro in macros)
			{
				addMacroToUndefine(macro);
			}
		}

		public string FilePath
		{
			set
			{
				// Only makes sense to set this once, a second set call is probably a mistake
				Debug.Assert(_fullPath == null);
				_fullPath = cleanPath(value);
			}
			get { return _fullPath; }
		}

		public string FileName
		{
			get
			{
				return Path.GetFileName(FilePath);
			}
		}

		public string RelativeFilePath
		{
			get { return cleanPath(_fullPath.Replace(_projectBasePath, "")); }
		}

		public string ProjectName
		{
			get { return _projectName; }
		}

		public string BaseProjectPath
		{
			set
			{
				// Only makes sense to set this once, a second set call is probably a mistake
				Debug.Assert(_projectBasePath == null);
				_projectBasePath = cleanPath(value);
			}
			get { return _projectBasePath; }
		}

		public List<string> IncludePaths
		{
			get { return _includePaths; }
		}

		public List<string> Macros
		{
			get { return _activeMacros; }
		}

		public List<string> MacrosToUndefine
		{
			get { return _macrosToUndefine; }
		}

		public VCCompilerVersion vcCompilerVersion
		{
			get { return _compilerVersion; }
		}

		private static string cleanPath(string path)
		{
			string result = path.Replace("\"", "");
			const string doubleBackSlash = "\\\\";
			const string singleBackSlash = "\\";
			if (result.StartsWith(doubleBackSlash))
			{
				// UNC path - must preserve the leading double slash
				result = singleBackSlash + result.Replace(doubleBackSlash, singleBackSlash);
			}
			else
			{
				result = result.Replace(doubleBackSlash, singleBackSlash);
			}

			if (result.EndsWith(singleBackSlash))
				result = result.Substring(0, result.Length - 1);

			if (result.StartsWith(singleBackSlash) && !result.StartsWith(doubleBackSlash))
				result = result.Substring(1);

			return result;
		}

		private string _fullPath        = null;
		private string _projectBasePath = null;
		private string _projectName     = null;
		private List<string> _includePaths = new List<string>();
		private List<string> _activeMacros = new List<string>();
		private List<string> _macrosToUndefine = new List<string>();
		private VCCompilerVersion _compilerVersion;
	}

	public class SourceFilesWithConfiguration {
		public IEnumerable<SourceFile> Files
		{
			get { return _files.Values; }
		}

		public EnvDTE.Configuration Configuration
		{
			get { return _configuration; }
			set
			{
				Debug.Assert(_configuration == null);
				Debug.Assert(value != null);
				_configuration = value;
			}
		}

		public int Count()
        {
			return _files.Count;
        }

		public bool Exists(string filePath)
        {
			return _files.ContainsKey(filePath);
        }

		public void addOrUpdateFile(SourceFile file)
		{
			if (file == null)
			{
				Debug.Fail("file is null!");
				return;
			}

			_files[file.FilePath] = file;
		}

		public void addMultipleFilesWithoutDuplicates(IEnumerable<SourceFile> files)
		{
			if (files == null)
			{
				Debug.Fail("files list is null!");
				return;
			}

			foreach (var file in files)
				addOrUpdateFile(file);
		}

		public async Task<bool> is64bitConfigurationAsync()
		{
			await CPPCheckPluginPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();
			return _configuration.PlatformName.Contains("64");
		}

		public async Task<bool> isDebugConfigurationAsync()
		{
			await CPPCheckPluginPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();
			return _configuration.ConfigurationName.ToLower().Contains("debug");
		}

		public bool Any()
		{
			return _files.Count != 0;
		}

		private Dictionary<string /* full path */, SourceFile> _files = new Dictionary<string, SourceFile>();
		private EnvDTE.Configuration _configuration;
	}
}
