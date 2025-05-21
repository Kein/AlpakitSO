using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using AutomationTool;
using EpicGames.Core;
using UnrealBuildTool;
using AutomationScripts;

namespace Alpakit.Automation
{
	public class SSParams
	{
		public bool CopyToGameDirectory;
		public bool StartGame;
		public string GameDirectory;
		public string LaunchGameURL;
	}

	public class PackagePlugin : BuildCommand
	{

		private static void CopyPluginToTheGameDir(string gameDir, FileReference projectFile, FileReference pluginFile,
			string stagingDir)
		{
			var modsDir = Path.Combine(gameDir, projectFile.GetFileNameWithoutAnyExtensions(), "Mods");
			var projectPluginsFolder =
				new DirectoryReference(Path.Combine(projectFile.Directory.ToString(), "Plugins"));
			var pluginRelativePath = pluginFile.Directory.MakeRelativeTo(projectPluginsFolder);

			var resultPluginDirectory = Path.Combine(modsDir, pluginRelativePath);
			if (DirectoryExists(resultPluginDirectory))
			{
				DeleteDirectory(resultPluginDirectory);
			}

			CreateDirectory(resultPluginDirectory);

			CopyDirectory_NoExceptions(stagingDir, resultPluginDirectory);
		}

		private static ProjectParams GetParams(BuildCommand cmd)
		{
			var projectFileName = cmd.ParseRequiredStringParam("Project");
			LogInformation(projectFileName);

			var pluginName = cmd.ParseRequiredStringParam("PluginName");
			var projectFile = new FileReference(projectFileName);

			if (string.IsNullOrEmpty(pluginName))
				throw new ArgumentException("-PluginName is required to package a mod/plugin");

			var projectParameters = new ProjectParams(
				projectFile,
				cmd,
				ClientTargetPlatforms: new List<TargetPlatformDescriptor>
				{
					new TargetPlatformDescriptor(UnrealTargetPlatform.Win64)
				},
				ClientConfigsToBuild: new List<UnrealTargetConfiguration>
				{
					UnrealTargetConfiguration.Shipping
				},

				// Alpakit shared configuration
				Cook: true,
				AdditionalCookerOptions: "-AllowUncookedAssetReferences -versioncookedcontent",
				UnversionedCookedContent: false,
				SkipCookingEditorContent: true,
				DLCIncludeEngineContent: false,
				Compressed: false,
				Pak: true,
				Stage: true,
				DLCName: pluginName,

				// TODO @SML: I would like to pass an empty based on release version, but the cooker checks against it
				BasedOnReleaseVersion: "NonExistentBasedOnReleaseVersion"
            );

			projectParameters.ValidateAndLog();
			return projectParameters;
		}

		private static void TryUpdateModulesFile(string filePath, string targetBuildId)
		{
			try
			{
				var modulesObject = JsonObject.Read(new FileReference(filePath));
				var modulesSubObject = modulesObject.GetObjectField("Modules");

				using (var writer = new JsonWriter(filePath))
				{
					writer.WriteObjectStart();
					writer.WriteValue("BuildId", targetBuildId);

					writer.WriteObjectStart("Modules");
					foreach (var moduleName in modulesSubObject.KeyNames)
					{
						var modulePath = modulesSubObject.GetStringField(moduleName);
						writer.WriteValue(moduleName, modulePath);
					}

					writer.WriteObjectEnd();
					writer.WriteObjectEnd();
				}
			}
			catch (Exception ex)
			{
				throw new AutomationException("Failed to update modules file '{0}': {1}", filePath, ex.Message);
			}
		}

		private static void UpdateModulesBuildId(string stagingDir, string targetBuildId)
		{
			var foundFiles = FindFiles("*.modules", true, stagingDir);

			foreach (var modulesFile in foundFiles)
			{
				TryUpdateModulesFile(modulesFile, targetBuildId);
			}
		}

		private static IReadOnlyList<DeploymentContext> CreateDeploymentContexts(ProjectParams projectParams)
		{
			var deployContextList = new List<DeploymentContext>();
			if (!projectParams.NoClient)
			{
				deployContextList.AddRange(Project.CreateDeploymentContext(projectParams, false));
			}

			if (projectParams.DedicatedServer)
			{
				deployContextList.AddRange(Project.CreateDeploymentContext(projectParams, true));
			}

			return deployContextList;
		}

		private static void RemapCookedPluginsContentPaths(ProjectParams projectParams, IEnumerable<DeploymentContext> deploymentContexts)
		{
			foreach (var deploymentContext in deploymentContexts)
			{
				//We need to make sure content paths will be relative to ../../../ProjectRoot/Mods/DLCFilename/Content,
				//because that's what will be used in runtime as a content path for /DLCFilename/ mount point.
				//But both project and engine plugins are actually cooked by different paths:
				//Project plugins expect to be mounted under ../../../ProjectRoot/Plugins/DLCFilename/Content,
				//and engine plugins expect to be mounted under ../../../Engine/Plugins/DLCFilename/Content
				//Since changing runtime content path is pretty complicated and impractical,
				//we remap cooked filenames to match runtime expectations. Since cooked assets only reference other assets
				//using mount point-relative paths, it doesn't need any changes inside of the cooked assets
				//deploymentContext.RemapDirectories.Add(Tuple.Create());

				var projectName = projectParams.RawProjectPath.GetFileNameWithoutAnyExtensions();

				if (string.IsNullOrEmpty(projectParams.DLCFile?.GetFileName()))
					throw new ArgumentException($"Expected DLCFile populated, got none (-PluginName is missing?)");

				string dlcSourceDirectory;
				if (projectParams.DLCFile.IsUnderDirectory(deploymentContext.EngineRoot))
					dlcSourceDirectory = Path.Combine("Engine", projectParams.DLCFile.Directory.ParentDirectory.MakeRelativeTo(deploymentContext.EngineRoot));
				else if (projectParams.DLCFile.IsUnderDirectory(deploymentContext.ProjectRoot))
					dlcSourceDirectory = Path.Combine(projectName, projectParams.DLCFile.Directory.ParentDirectory.MakeRelativeTo(deploymentContext.ProjectRoot));
				else
					throw new Exception("Unknown DLC remap for DLC " + projectParams.DLCFile.GetFileNameWithoutExtension());

				var destinationModsDir = Path.Combine(projectName, "Mods");
				
				deploymentContext.RemapDirectories.Add(Tuple.Create(
					new StagedDirectoryReference(dlcSourceDirectory), 
					new StagedDirectoryReference(destinationModsDir)));
			}
		}

		public static void CopyBuildToStagingDirectory(ProjectParams Params, IReadOnlyList<DeploymentContext> deploymentContexts) {
			var platformCares = Params.ClientTargetPlatformInstances[0].RequiresPak(Params);
			var requiresPak = platformCares == Platform.PakType.Always || (Params.Pak && platformCares != Platform.PakType.Never);
			
			if (requiresPak || (Params.Stage && !Params.SkipStage)) {
				LogInformation("********** STAGE COMMAND STARTED **********");

				// clean the staging directories first
				foreach (var sc in deploymentContexts) {
					Project.CreateStagingManifest(Params, sc);
					Project.CleanStagingDirectory(Params, sc);
				}

				foreach (var sc in deploymentContexts) {
					Project.ApplyStagingManifest(Params, sc);
				}

				LogInformation("********** STAGE COMMAND COMPLETED **********");
			}
		}

		private static void PackagePluginProject(IEnumerable<DeploymentContext> deploymentContexts,
			string workingBuildId)
		{
			foreach (var deploymentContext in deploymentContexts)
			{
				//Update .modules files build id to match game's one before packaging
				UpdateModulesBuildId(deploymentContext.StageDirectory.ToString(), workingBuildId);
			}
		}

		private static string GetPluginPathRelativeToStageRoot(ProjectParams projectParams, DeploymentContext SC)
		{
			// All DLC paths are remapped to projectName/Mods/DLCName during RemapCookedPluginsContentPaths, regardless of nesting
			// so the relative stage path is projectName/Mods/DLCName
			var projectName = projectParams.RawProjectPath.GetFileNameWithoutAnyExtensions();
			var dlcName = projectParams.DLCFile.GetFileNameWithoutAnyExtensions();
			return Path.Combine(projectName, "Mods", dlcName);
		}

		private static void ArchivePluginProject(ProjectParams projectParams,
			IEnumerable<DeploymentContext> deploymentContexts)
		{
			var baseArchiveDirectory = CombinePaths(Path.GetDirectoryName(projectParams.RawProjectPath.ToString()),
				"Saved", "ArchivedPlugins");

			foreach (var deploymentContext in deploymentContexts)
			{
				var stageRootDirectory = deploymentContext.StageDirectory;
				var relativePluginPath = GetPluginPathRelativeToStageRoot(projectParams, deploymentContext);
				var stagePluginDirectory = Path.Combine(stageRootDirectory.ToString(), relativePluginPath);

				var archiveDirectory = Path.Combine(baseArchiveDirectory, deploymentContext.FinalCookPlatform);
				CreateDirectory(archiveDirectory);

				var zipFilePath = Path.Combine(archiveDirectory,
					projectParams.DLCFile.GetFileNameWithoutAnyExtensions() + ".zip");
				if (FileExists(zipFilePath))
				{
					DeleteFile(zipFilePath);
				}

				ZipFile.CreateFromDirectory(stagePluginDirectory, zipFilePath);
			}
		}

		private static void DeployPluginProject(ProjectParams projectParams,
			IEnumerable<DeploymentContext> deploymentContexts, SSParams SSParams)
		{
			foreach (var deploymentContext in deploymentContexts)
			{
				if (SSParams.CopyToGameDirectory && deploymentContext.FinalCookPlatform == "Windows")
				{
					var stageRootDirectory = deploymentContext.StageDirectory;
					var relativePluginPath = GetPluginPathRelativeToStageRoot(projectParams, deploymentContext);
					var stagePluginDirectory = Path.Combine(stageRootDirectory.ToString(), relativePluginPath);

					if (SSParams.GameDirectory == null)
						throw new AutomationException("-CopyToGameDirectory was specified, but no game directory path has been provided");
					
					CopyPluginToTheGameDir(SSParams.GameDirectory, projectParams.RawProjectPath,
						projectParams.DLCFile, stagePluginDirectory);
				}
			}

			if (SSParams.StartGame && !string.IsNullOrEmpty(SSParams.LaunchGameURL))
				System.Diagnostics.Process.Start(new ProcessStartInfo(SSParams.LaunchGameURL) { UseShellExecute = true });
		}

		private static void CleanStagingDirectories(IEnumerable<DeploymentContext> deploymentContexts)
		{
			foreach (var deploymentContext in deploymentContexts)
				if (DirectoryExists(deploymentContext.StageDirectory.ToString()))
					DeleteDirectory(deploymentContext.StageDirectory);
		}

		public override void ExecuteBuild()
        {
			var projectParams = GetParams(this);
			var SSParams = new SSParams();
			string projectName = projectParams.RawProjectPath.GetFileNameWithoutAnyExtensions();
			string gameDirPath = ParseOptionalStringParam("GameDir");

			if (!string.IsNullOrEmpty(gameDirPath))
			{
				var gameDir = new DirectoryInfo(gameDirPath);
				var contentRoot = Path.Combine(gameDir.FullName, projectName);
				if (!DirectoryExists(contentRoot))
					throw new AutomationException("Provided -GameDir is invalid, expected to find {0}", contentRoot);

				SSParams.GameDirectory = gameDir.FullName ?? string.Empty;
				SSParams.StartGame = ParseParam("LaunchGame");
				SSParams.CopyToGameDirectory = ParseParam("CopyToGameDir");

				if (SSParams.StartGame)
				{
					// In 99% cases, UE games have projectName equal to the root project content folder,
					// this way we can validate if we are in the correct game folder.
					var allEXEs = gameDir.GetFiles("*.exe", new EnumerationOptions() { RecurseSubdirectories = false });
					SSParams.LaunchGameURL = allEXEs[0]?.FullName ?? string.Empty;
					if (Path.GetFileNameWithoutExtension(SSParams.LaunchGameURL) != projectName)
						LogWarning($"Mismatch between executable name and project name: '{Path.GetFileNameWithoutExtension(SSParams.LaunchGameURL)}' != '{projectName}'");
				}
			}

			Project.Cook(projectParams);
			var deploymentContexts = CreateDeploymentContexts(projectParams);
			RemapCookedPluginsContentPaths(projectParams, deploymentContexts);

			try
			{
				CopyBuildToStagingDirectory(projectParams, deploymentContexts);
				PackagePluginProject(deploymentContexts, "SZML");
				ArchivePluginProject(projectParams, deploymentContexts);
				DeployPluginProject(projectParams, deploymentContexts, SSParams);
			}
			finally
			{
				//Clean staging directories because they confuse cooking commandlet and UBT
				CleanStagingDirectories(deploymentContexts);
			}
		}
	}
}