using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSPackage.CPPCheckPlugin
{
	public class Problem
	{
		public enum SeverityLevel { info, warning, error };

		public Problem(ICodeAnalyzer analyzer, SeverityLevel severity, String messageId, String message, String file, int line, String baseProjectPath, String projectName)
		{
			_analyzer  = analyzer;
			_severity  = severity;
			_messageId = messageId;
			_message   = message;
			_file      = file;
			_line      = line;
			_baseProjectPath = baseProjectPath;
			_projectName = projectName;
		}

		public ICodeAnalyzer Analyzer
		{
			get { return _analyzer; }
		}

		public SeverityLevel Severity
		{
			get { return _severity; }
		}
		
		public String MessageId
		{
			get { return _messageId; }
		}

		public String Message
		{
			get { return _message; }
		}

		// This should be file path relative to project root
		public String FullFileName
		{
			get {  return _file; }
		}

		// file name only without path
		public String FileName
		{
			get
			{
				return System.IO.Path.GetFileName(_file);
			}
		}

		public String FilePath
		{
			get {
				if (String.IsNullOrWhiteSpace(_file))
					return _file;
				else if (_file.Contains(":")) // Absolute path
					return _file; 
				else
				{
					if (String.IsNullOrWhiteSpace(_baseProjectPath))
						return _file;
					// Relative path - making absolute
					return _baseProjectPath.EndsWith("\\") ? _baseProjectPath + _file : _baseProjectPath + "\\" + _file;
				}
			}
		}

		public int Line
		{
			get { return _line; }
		}

		public String BaseProjectPath
		{
			get { return _baseProjectPath; }
		}

		public String ProjectName
		{
			get { return _projectName; }
		}

		private ICodeAnalyzer _analyzer;
		private SeverityLevel _severity;
		private String        _messageId;
		private String        _message;
		private String        _file;
		private String        _baseProjectPath;
		private String        _projectName;
		private int           _line;
	}
}
