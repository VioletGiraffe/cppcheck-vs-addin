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

        public static readonly Guid guidCPPCheckPluginCmdSet = new Guid(guidCPPCheckPluginCmdSetString);
    };
}