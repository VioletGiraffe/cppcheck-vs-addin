using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;

namespace VSPackage.CPPCheckPlugin
{
	public class ConfiguredFiles
	{
		private List<SourceFile> files;
		public List<SourceFile> Files
		{
			get { return files; }
			set { files = value; }
		}

		private Configuration configuration;
		public Configuration Configuration
		{
			get { return configuration; }
			set { configuration = value; }
		}

		public bool is64bitConfiguration()
		{
			return Configuration.ConfigurationName.Contains("64");
		}
		public bool isDebugConfiguration()
		{
			return Configuration.ConfigurationName.ToLower().Contains("debug");
		}
	}
}
