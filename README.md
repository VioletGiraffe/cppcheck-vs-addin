# Visual Studio integration add-in for Cppcheck

This project is archived because I'm no longer interested in using or maintaining it. Feel free to fork and continue the development.

[![Build status](https://ci.appveyor.com/api/projects/status/8qe1iy0ook8rme9o?svg=true)](https://ci.appveyor.com/project/VioletGiraffe/cppcheck-vs-addin)

[Cppcheck](http://cppcheck.sourceforge.net/) is a C and C++ source code static analysis tool.

This plugin integrates Cppcheck into Visual Studio and allows:

 * automatically checking every C / C++ source file upon saving;
 * checking the currently selected project in the Solution Explorer (menu -> Tools -> Check current project);
 * convenient message suppression management with options to suppress specific messages, all messages in a given file, specific message types in a given file, message types globally, solution-wide and project-wide.

### Download
Visual Studio 2022 is supported. VS2019, 2017 and 2015 are supported by the older releases.

**<a href="https://github.com/VioletGiraffe/cppcheck-vs-addin/releases/latest">Get the latest release</a>**

NOTE: The add-in does not deploy Cppcheck executable. Please, go to [Cppcheck](http://cppcheck.sourceforge.net/) website, download the installer and install it before first use of the add-in. The add-in then may prompt for location of the `cppcheck.exe`.

### Contributors

Should you decide to open, build and debug the project please follow these steps:

Use Visual Studio 2022 - ensure you have the workload for Extension Development installed. All SDKs are referenced as nuget packages and should 
 
 * Press F5 (*Debug* -> *Start Debugging*) to have the project built and deployed into "Experimental Instance" of Visual Studio.
 This should start another ("experimental") instance of Visual Studio *of the same version* with the addin deployed there.

  * If the project builds fine but "Experimental instance" does not start
(you get *Visual Studio cannot start debugging because the debug target is missing*) message
or the wrong Visual Studio version is started do the following:
  * right-click the project in Solution Explorer and get to *Properties*
  * get to *Debug* tab
  * next to *start external program* alter the path so that it points to where the right version of Visual Studio is installed. Path should be something like *"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe"*
  * In the same window add */rootsuffix Exp* to *Command line arguments*

