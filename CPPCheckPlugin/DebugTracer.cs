using System;
using System.Diagnostics;

namespace VSPackage.CPPCheckPlugin
{
	class DebugTracer
	{
		[Conditional("DEBUG")]
		public static void Trace(Exception ex)
		{
			Debug.WriteLine("Exception occurred in cppcheck add-in: " + ex.Message);
		}
	}
}
