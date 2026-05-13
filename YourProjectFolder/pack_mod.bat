@echo off
setlocal
SET r=[31m
SET w=[37m
set UATPath=..\Engine\Binaries\DotNET\AutomationTool\AutomationTool.exe
set ProjectFile=
set ProjectDir=%~dp0
set OutputParam=-CopyToGameDir -GameDir=%~2


IF NOT EXIST %UATPath% (
    echo %r%Suppled path is not a valid path to to AutomationTool.exe:%w%
    echo %UATPath%
    Exit /B
)

IF "%~1"=="" (
    echo Mod name is required!
    Exit /B
)

IF "%~2"=="" (
    set OutputParam=
    echo Game folder not specified, packing into Saved/ArchivedPlugins
)

IF "%ProjectFile%"=="" (FOR %%F IN (*.uproject) DO (set ProjectFile=%%F))


IF NOT EXIST %ProjectFile% (
    echo %r%.uproject file not found or path invalid:%w%
    echo %ProjectFile%
    Exit /B
)

echo Packaging %ProjectDir%%ProjectFile%Mods\%~1
%UATPath% PackagePlugin -Project=%ProjectDir%%ProjectFile% -PluginName=%~1 -nocompile -nocompileuat -versioncookedcontent
Exit /B
