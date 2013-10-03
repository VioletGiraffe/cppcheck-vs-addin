using System.Collections.Generic;
using System.Text;
using System;
using System.Diagnostics;

namespace VSPackage.CPPCheckPlugin
{
    class SourceFile
    {
        public SourceFile(string fullPath, string projectBasePath)
        {
            _fullPath = cleanPath(fullPath);
            _projectBasePath = cleanPath(projectBasePath);
        }

        // All include paths being added are resolved against projectBasePath
        public void addIncludePath(string path)
        {
            if (!String.IsNullOrEmpty(_projectBasePath))
                _includePaths.Add(cleanPath(path.Contains(":") ? path : (_projectBasePath + path)));
        }

        public void addIncludePaths(List<string> paths)
        {
            foreach (string path in paths)
            {
                addIncludePath(path);
            }
        }

        public void addMacro(string macro)
        {
            _activeMacros.Add(macro);
        }

        public void addMacros(List<string> macros)
        {
            foreach (string macro in macros)
            {
                addMacro(macro);
            }
        }

        public string FilePath
        {
            set { Debug.Assert(_fullPath == null); _fullPath = cleanPath(value); } // Only makes sense to set this once, a second set call is probably a mistake
            get { return _fullPath; }
        }

        public string BaseProjectPath
        {
            set { Debug.Assert(_projectBasePath == null); _projectBasePath = cleanPath(value); } // Only makes sense to set this once, a second set call is probably a mistake
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

        private static string cleanPath(string path)
        {
            string result = path.Replace("\"", "").Replace("\\\\", "\\");
            if (result.EndsWith("\\"))
                result = result.Substring(0, result.Length - 1);
            if (result.StartsWith("\\"))
                result = result.Substring(1);
            return result;
        }

        private string _fullPath = null;
        private string _projectBasePath = null;
        private List<string> _includePaths = new List<string>();
        private List<string> _activeMacros = new List<string>();
    }
}
