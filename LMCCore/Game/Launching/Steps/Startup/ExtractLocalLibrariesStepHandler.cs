// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using System.IO.Compression;
using System.Runtime.InteropServices;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Launching.Execution;
using LMCCore.Game.Model.LocalVersion.Compatibility;
using LMCCore.Game.Model.LocalVersion.Libraries;

namespace LMCCore.Game.Launching.Steps.Startup;

public sealed class ExtractLocalLibrariesStepHandler : IGameLaunchStepHandler
{
    public GameLaunchProgressStep Step => GameLaunchProgressStep.ExtractLocalLibraries;

    public async Task ExecuteAsync(GameLaunchContext context, CancellationToken cancellationToken)
    {
        var nativesFolder = Path.Combine(context.Version.VersionDirectory, $"natives-{RuntimeInformation.RuntimeIdentifier}");
        if (Directory.Exists(nativesFolder))
        {
            Directory.Delete(nativesFolder, true);
        }
        Directory.CreateDirectory(nativesFolder);

        if (context.Version.VersionInfo == null)
        {
            return;
        }

        var nativeArchives = CollectNativeArchivePaths(context);
        foreach (var nativeArchive in nativeArchives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExtractNativeFilesAsync(nativeArchive, nativesFolder, cancellationToken);
        }
    }

    private static IReadOnlyCollection<string> CollectNativeArchivePaths(GameLaunchContext context)
    {
        var nativeArchives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentOs = PlatformDetector.GetCurrentOs();

        foreach (var library in context.Version.VersionInfo!.Libraries)
        {
            switch (library)
            {
                case SimpleLibraryInfo simpleLibrary:
                    AddSimpleLibraryNativeArchive(context, simpleLibrary, nativeArchives);
                    break;

                case LibraryInfo detailedLibrary:
                    AddDetailedLibraryNativeArchives(context, detailedLibrary, currentOs, nativeArchives);
                    break;
            }
        }

        return nativeArchives;
    }

    private static void AddSimpleLibraryNativeArchive(
        GameLaunchContext context,
        SimpleLibraryInfo library,
        ISet<string> nativeArchives)
    {
        return;
        
        // Unused
        if (!library.Name.Contains("natives", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!VanillaGameDownloader.TryBuildMavenRelativePath(library.Name, out var relativePath))
        {
            return;
        }

        nativeArchives.Add(VanillaGameDownloader.GetLibrarySavePath(context.Version.RootPath, relativePath));
    }

    private static void AddDetailedLibraryNativeArchives(
        GameLaunchContext context,
        LibraryInfo library,
        string currentOs,
        ISet<string> nativeArchives)
    {
        if (!CompatibilityRuleEvaluator.CheckRulesApply(library.Rules))
        {
            return;
        }
        
        // if (library.Name.Contains("natives", StringComparison.OrdinalIgnoreCase))
        // {
        //     if (library.Name.Contains(":natives-"))
        //     {
        //         var i = library.Name.LastIndexOf(':');
        //         var nativesIdentifier = library.Name[(i + 1)..]
        //             .Replace("natives-", "");
        //         var rule = new CompatibilityRule
        //         {
        //             Action = "allow",
        //             Os = new RuleOs
        //             {
        //                 Name = nativesIdentifier
        //             }
        //         };
        //         if (!CompatibilityRuleEvaluator.RuleMatchesOs(rule)) return;
        //     }
        //     var archivePath = library.Downloads?.Artifact?.Path ?? library.Path;
        //     if (!string.IsNullOrWhiteSpace(archivePath))
        //     {
        //         nativeArchives.Add(VanillaGameDownloader.GetLibrarySavePath(context.Version.RootPath, archivePath));
        //     }
        // }

        if (library.Natives is not { Count: > 0 } ||
            library.Downloads?.Classifiers is not { Count: > 0 } ||
            !library.Natives.TryGetValue(currentOs, out var classifierKey) ||
            !library.Downloads.Classifiers.TryGetValue(classifierKey, out var nativeFile) ||
            string.IsNullOrWhiteSpace(nativeFile.Path))
        {
            return;
        }

        nativeArchives.Add(VanillaGameDownloader.GetLibrarySavePath(context.Version.RootPath, nativeFile.Path));
    }

    async private static Task ExtractNativeFilesAsync(
        string archivePath,
        string nativesFolder,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(archivePath))
        {
            return;
        }

        await using var archive = await ZipFile.OpenReadAsync(archivePath, cancellationToken);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(entry.Name) || !IsNativeLibraryFile(entry.Name))
            {
                continue;
            }

            var targetPath = Path.Combine(nativesFolder, entry.FullName);
            var targetPathFlat = Path.Combine(nativesFolder, entry.Name);
            if (!Directory.Exists(Path.GetDirectoryName(targetPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? throw new NullReferenceException());
            }
            await using var entryStream = await entry.OpenAsync(cancellationToken);
            // await using (var fileStream = new FileStream(
            //                  targetPath,
            //                  FileMode.Create,
            //                  FileAccess.Write,
            //                  FileShare.None,
            //                  81920,
            //                  useAsync: true))
            // {
            //     await entryStream.CopyToAsync(fileStream, cancellationToken);
            // }

            await using var fileStreamFlat = new FileStream(
                targetPathFlat,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true);
            await entryStream.CopyToAsync(fileStreamFlat, cancellationToken);
        }
    }

    private static bool IsNativeLibraryFile(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".so", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".dylib", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jnilib", StringComparison.OrdinalIgnoreCase);
    }
}
