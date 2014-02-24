using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace VSPackage.CPPCheckPlugin
{
	class DebugTracer
	{
		// [Conditional("DEBUG")]
		public static void Trace(Exception ex)
		{
			Debug.WriteLine("Exception occurred in cppcheck add-in: " + ex.ToString());
			MessageBox.Show("Exception occurred in cppcheck add-in", ex.ToString());
		}
	}
}
