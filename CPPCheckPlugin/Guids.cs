// Guids.cs
// MUST match guids.h
using System;

namespace VSPackage.CPPCheckPlugin
{
	static class GuidList
	{
		public const string guidCPPCheckPluginPkgString = "127d8bd3-8cd7-491a-9a63-9b4e89118da9";
		public const string guidCPPCheckPluginCmdSetString = "7fcb87ef-4f0c-4713-8217-5bef43dc0de4";
		public const string guidToolWindowPersistanceString = "7cc7c6f9-a686-4108-abf5-fab92cd024bc";
		public const string guidCPPCheckPluginProjectCmdSetString = "9019df3f-c4aa-499b-b46a-0bcb43d006db";
		public const string guidCPPCheckPluginMultiProjectCmdSetString = "a21f0125-e59b-4ac6-8db6-fc01601237d7";
		public const string guidCPPCheckPluginMultiItemProjectCmdSetString = "6db8495c-52d3-45d8-ba02-09a3938aa893";

		public static readonly Guid guidCPPCheckPluginCmdSet = new Guid(guidCPPCheckPluginCmdSetString);
		public static readonly Guid guidCPPCheckPluginProjectCmdSet = new Guid(guidCPPCheckPluginProjectCmdSetString);
		public static readonly Guid guidCPPCheckPluginMultiProjectCmdSet = new Guid(guidCPPCheckPluginMultiProjectCmdSetString);
		public static readonly Guid guidCPPCheckPluginMultiItemProjectCmdSet = new Guid(guidCPPCheckPluginMultiItemProjectCmdSetString);
	};
}