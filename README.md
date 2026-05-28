# AlpakitSO
1. Copy the `Engine` folder inside your UE root installation or, alternatively, manually copy the `PackagePlugin.cs` where it needs to be as per repo structure.
2. Re-build `AutomationScripts.Automation.csproj` (even if you have Launcher UE build - all .Net tools come with source):
   `dotnet build X:\...\Engine\Source\Programs\AutomationTool\Scripts\AutomationScripts.Automation.csproj`
3. Re-build Automation Tool itself: `dotnet build X:\...\Engine\Source\Programs\AutomationTool\AutomationTool.csproj`
4. Copy the content of `YourProjectFolder\` inside your game project mockup
5. Rename and update the Example Plugin
6. See provided BAT file on how to package the mod manually from CLI
7. Alternatively to #5, install Alpakit plugin from this repo and rebuild game project (do NOT install as Engine plugin) and use toolbar menu to package straight into game directory

# Credits
Alpakit courtesy of https://github.com/satisfactorymodding/SatisfactoryUnrealProject (hard fork)  
Changes and updates: KZ
