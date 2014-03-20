#Visual Studio integration add-in for Cppcheck

[Cppcheck](http://cppcheck.sourceforge.net/) is a C and C++ source code static analysis tool.

This plugin integrates Cppcheck into Visual Studio and allows:

 * automatically checking every C / C++ source file upon saving;
 * checking the currently selected project in the Solution Explorer (menu -> Tools -> Check current project);
 * convenient message suppression management with options to suppress specific messages, all messages in a given file, specific message types in a given file, message types globally, solution-wide and project-wide.

###Download
Visual Studio 2012 and 2013 (except Express editions) supported.

**<a href="https://github.com/VioletGiraffe/cppcheck-vs-addin/releases/latest">Get the latest release</a>**

###Contributors

Should you decide to open, build and debug the project please follow these steps:

 * Install Visual Studio SDK for your version of Visual Studio.
If you plan to open the project in more that one supported version you'll have to install
the matching Visual Studio SDK for each version of Visual Studio separately.
<a href="http://www.microsoft.com/en-us/download/details.aspx?id=30668">Here's Visual Studio 2012 SDK</a>
and <a href="http://www.microsoft.com/en-us/download/details.aspx?id=40758">here's Visual Studio 2013 SDK</a>.

  * Note: if the Visual Studio instance in which you planned to open the project was running
 while you were installing the SDK you'll have to restart
 that Visual Studio instance - otherwise the project won't open.

 * Once Visual Studio SDK has been installed you can start Visual Studio and open the project.

 * Press F5 (*Debug* -> *Start Debugging*) to have the project built and deployed into "Experimental Instance" of Visual Studio.
 This should start another ("experimental") instance of Visual Studio *of the same version* with the addin deployed there.

  * If the project builds fine but "Experimental instance" does not start
(you get *Visual Studio cannot start debugging because the debug target is missing*) message
or the wrong Visual Studio version is started do the following:
  * right-click the project in Solution Explorer and get to *Properties*
  * get to *Debug* tab
  * next to *start external program* alter the path so that it points to where the right version of Visual Studio is installed. Path should be something like *"C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\devenv.exe"*
