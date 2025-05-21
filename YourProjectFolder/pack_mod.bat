@echo off
setlocal
set UATPath=..\Engine\Binaries\DotNET\AutomationTool\AutomationTool.exe
set ProjectFile=
set ProjectDir=%~dp0

IF "%~1"=="" (GOTO FAIL)

FOR %%F IN (*.uproject) DO (
  set ProjectFile=%%F
  goto MAIN
)
:MAIN
echo Packaging %ProjectDir%%ProjectFile%Mods\%~1

%UATPath% PackagePlugin -Project=%ProjectDir%%ProjectFile% -PluginName=%~1 -nocompile -nocompileuat
tree %~dp0Saved\ArchivedPlugins\
Exit /B

:FAIL
echo Mod name is required
Exit /B