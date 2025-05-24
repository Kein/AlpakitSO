@echo off
setlocal
set UATPath=..\Engine\Binaries\DotNET\AutomationTool\AutomationTool.exe
set ProjectFile=
set ProjectDir=%~dp0
set OutputParam=-CopyToGameDir -GameDir=%~2

IF "%~1"=="" (
    echo Mod name is required!
    Exit /B
)

IF "%~2"=="" (set OutputParam=)

FOR %%F IN (*.uproject) DO (set ProjectFile=%%F)
IF "%ProjectFile%"=="" (
    echo Failed to find uproject file!
    Exit /B
)

echo Packaging %ProjectDir%%ProjectFile%Mods\%~1
%UATPath% PackagePlugin -Project=%ProjectDir%%ProjectFile% -PluginName=%~1 -nocompile -nocompileuat -versioncookedcontent
Exit /B
