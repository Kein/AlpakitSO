# AlpakitSO
1. Copy the `Engine` inside your UE root installation or, alternatively, manually copy the `PackagePlugin.cs` where it needs to be as per repo structure.
2. Re-build `AutomationScripts.Automation.csproj` (even if you have Launcher UE build - all .Net tools come with source):
   `dotnet build X:\...\Engine\Source\Programs\AutomationTool\Scripts\AutomationScripts.Automation.csproj`
4. See provided BAT file on how to package the mod manually
5. Alternatively to #4, install Alpakit plugin from this repo and rebuild game project (do NOT install as Engine plugin)

# Credits
Alpakit courtesy of https://github.com/satisfactorymodding/SatisfactoryUnrealProject (hard fork)
