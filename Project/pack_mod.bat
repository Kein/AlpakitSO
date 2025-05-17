@echo off
set UATPath=..\Engine\Binaries\DotNET\AutomationTool\AutomationTool.exe
set ProjectFile=YOUR.uproject
set ProjectDir=%~dp0
setlocal

IF "%~1"=="" (GOTO FAIL)
echo Packaging %ProjectDir%%ProjectFile%Mods\%~1
%UATPath% PackagePlugin -Project=%ProjectDir%%ProjectFile% -PluginName=%~1 -GameDir= -nocompile -nocompileuat
tree %~dp0Saved\ArchivedPlugins\
Exit /B

:FAIL
Echo Mod name is required
Exit /B