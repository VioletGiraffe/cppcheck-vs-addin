// PkgCmdID.cs
// MUST match PkgCmdID.h
using System;

namespace VSPackage.CPPCheckPlugin
{
    static class PkgCmdIDList
    {
        public const uint cmdidCheckProjectCppcheck = 0x101;
        public const uint cmdidCheckMultiItemCppcheck = 0x102;
        public const uint cmdidStopCppcheck = 0x103;
        public const uint cmdidSettings = 0x104;
        
        public const uint cmdidCheckProjectCppcheck1 = 0x105;
        public const uint cmdidCheckProjectsCppcheck = 0x106;
        public const uint cmdidCheckMultiItemCppcheck1 = 0x107;
    };
}