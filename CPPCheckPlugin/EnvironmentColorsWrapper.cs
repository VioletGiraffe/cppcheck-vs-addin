using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace VSPackage.CPPCheckPlugin
{
    public static class EnvironmentColorsWrapper
    {
        public static ThemeResourceKey ToolWindowBackgroundBrushKey
        {
            get
            {
                return EnvironmentColors.ToolWindowBackgroundBrushKey;
            }
        }
        public static ThemeResourceKey ToolWindowTextBrushKey
        {
            get
            {
                return EnvironmentColors.ToolWindowTextBrushKey;
            }
        }
        public static ThemeResourceKey GridHeadingBackgroundBrushKey
        {
            get
            {
                return EnvironmentColors.GridHeadingBackgroundBrushKey;
            }
        }

        public static ThemeResourceKey GridHeadingTextBrushKey
        {
            get
            {
                return EnvironmentColors.GridHeadingTextBrushKey;
            }
        }
    }
}
