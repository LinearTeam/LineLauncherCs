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

using System.Diagnostics;
using System.ComponentModel;
using LMC.Basic.Logging;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Utils;

namespace LMCCore.Game.Download.Installation.Forge;

internal sealed class ForgeProcessorExecutor(Logger logger)
{
    private readonly Logger _logger = logger;

    public async Task ExecuteProcessorsAsync(
        DownloadInstallationContext context,
        ForgeInstallationRuntimeState forgeState,
        CancellationToken cancellationToken,
        IProgress<int> progress)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(forgeState);

        var installProfile = JsonUtils.Parse(forgeState.InstallProfileJson).Get<ForgeInstallProfile>()
                             ?? throw new InvalidOperationException("Failed to parse install_profile.json.");
        var processors = installProfile.Processors ?? [];
        if (processors.Count == 0)
        {
            progress.Report(100);
            return;
        }

        var java = await ForgeJavaSelector.SelectJavaAsync(cancellationToken);
        var javaExecutable = ForgeJavaSelector.GetJavaExecutablePath(java);

        for (var index = 0; index < processors.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var processor = processors[index];
            if (!ShouldRunOnClient(processor))
            {
                progress.Report((index + 1) * 100 / processors.Count);
                continue;
            }

            if (IsDownloadMojmapsTask(processor))
            {
                await DownloadMojmapsAsync(context, forgeState, installProfile, cancellationToken);
                progress.Report((index + 1) * 100 / processors.Count);
                continue;
            }

            await ExecuteProcessorAsync(
                context,
                forgeState,
                installProfile,
                processor,
                javaExecutable,
                cancellationToken);
            progress.Report((index + 1) * 100 / processors.Count);
        }
    }

    async private Task ExecuteProcessorAsync(
        DownloadInstallationContext context,
        ForgeInstallationRuntimeState forgeState,
        ForgeInstallProfile installProfile,
        ForgeProcessorDefinition processor,
        string javaExecutable,
        CancellationToken cancellationToken)
    {
        var processorJarPath = ForgeLibraryPathHelper.GetAbsoluteLibraryPath(context.Request.RootPath, processor.Jar);
        var mainClass = ForgeInstallerArchiveReader.TryReadMainClassFromJar(processorJarPath)
                        ?? throw new InvalidOperationException($"Main-Class was not found in '{processor.Jar}'.");
        var classpath = BuildClasspath(context.Request.RootPath, processor);
        var arguments = await ResolveArgumentsAsync(
            context,
            forgeState,
            installProfile,
            processor,
            cancellationToken);
        var processArguments = $"-cp \"{classpath}\" {mainClass} {string.Join(' ', arguments.Select(EscapeArgument))}";

        _logger.Info($"Executing Forge processor '{processor.Jar}' with main class '{mainClass}'.");
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = javaExecutable,
            Arguments = processArguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                _logger.Debug($"Forge processor [{processor.Jar}] STDOUT: {args.Data}");
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                _logger.Warn($"Forge processor [{processor.Jar}] STDERR: {args.Data}");
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start Forge processor '{processor.Jar}'.");
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            _logger.Warn($"Cancellation requested for Forge processor '{processor.Jar}', terminating process tree.");
            TryTerminateProcess(process, processor.Jar);
        });

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryTerminateProcess(process, processor.Jar);

            try
            {
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                _logger.Warn($"Waiting for canceled Forge processor '{processor.Jar}' to exit failed: {ex}");
            }

            throw;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Forge processor '{processor.Jar}' failed with exit code {process.ExitCode}.");
        }

        _logger.Info($"Forge processor '{processor.Jar}' completed successfully.");
    }

    private static bool ShouldRunOnClient(ForgeProcessorDefinition processor)
    {
        return processor.Sides == null ||
               processor.Sides.Count == 0 ||
               processor.Sides.Contains("client", StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsDownloadMojmapsTask(ForgeProcessorDefinition processor)
    {
        if (processor.Args == null)
        {
            return false;
        }

        for (var i = 0; i < processor.Args.Count - 1; i++)
        {
            if (string.Equals(processor.Args[i], "--task", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(processor.Args[i + 1], "DOWNLOAD_MOJMAPS", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildClasspath(string rootPath, ForgeProcessorDefinition processor)
    {
        var classpathEntries = (processor.Classpath ?? [])
            .Select(lib => ForgeLibraryPathHelper.GetAbsoluteLibraryPath(rootPath, lib))
            .ToList();
        classpathEntries.Add(ForgeLibraryPathHelper.GetAbsoluteLibraryPath(rootPath, processor.Jar));
        return string.Join(Path.PathSeparator, classpathEntries);
    }

    async private Task<List<string>> ResolveArgumentsAsync(
        DownloadInstallationContext context,
        ForgeInstallationRuntimeState forgeState,
        ForgeInstallProfile installProfile,
        ForgeProcessorDefinition processor,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>();
        foreach (var arg in processor.Args ?? [])
        {
            arguments.Add(await ResolveArgumentAsync(context, forgeState, installProfile, arg, cancellationToken));
        }

        return arguments;
    }

    async private Task<string> ResolveArgumentAsync(
        DownloadInstallationContext context,
        ForgeInstallationRuntimeState forgeState,
        ForgeInstallProfile installProfile,
        string rawArg,
        CancellationToken cancellationToken)
    {
        if (IsWrapped(rawArg, '{', '}'))
        {
            return await ResolvePlaceholderAsync(
                context,
                forgeState,
                installProfile,
                rawArg[1..^1],
                cancellationToken);
        }

        if (IsWrapped(rawArg, '[', ']'))
        {
            var lib = rawArg[1..^1];
            return ForgeLibraryPathHelper.GetAbsoluteLibraryPath(context.Request.RootPath, lib);
        }

        return rawArg;
    }

    async private Task<string> ResolvePlaceholderAsync(
        DownloadInstallationContext context,
        ForgeInstallationRuntimeState forgeState,
        ForgeInstallProfile installProfile,
        string key,
        CancellationToken cancellationToken)
    {
        return key switch
        {
            "MINECRAFT_JAR" => context.CacheManager.CachedClientJarPath,
            "BINPATCH" => ForgeLibraryPathHelper.GetAbsoluteLibraryPath(
                context.Request.RootPath,
                $"net.minecraftforge:forge:{forgeState.LoaderVersion}:clientdata@lzma"),
            "INSTALLER" => forgeState.InstallerJarPath,
            "SIDE" => "client",
            "ROOT" => context.Request.RootPath,
            _ => await ResolveFromInstallProfileDataAsync(context, installProfile, key, cancellationToken)
        };
    }

    async private static Task<string> ResolveFromInstallProfileDataAsync(
        DownloadInstallationContext context,
        ForgeInstallProfile installProfile,
        string key,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (installProfile.Data == null ||
            !installProfile.Data.TryGetValue(key, out var entry) ||
            string.IsNullOrWhiteSpace(entry.Client))
        {
            throw new KeyNotFoundException($"Forge install_profile data entry '{key}' was not found.");
        }

        var value = entry.Client.Trim();
        if (IsWrapped(value, '[', ']'))
        {
            return ForgeLibraryPathHelper.GetAbsoluteLibraryPath(context.Request.RootPath, value[1..^1]);
        }

        return value;
    }

    async private Task DownloadMojmapsAsync(
        DownloadInstallationContext context,
        ForgeInstallationRuntimeState forgeState,
        ForgeInstallProfile installProfile,
        CancellationToken cancellationToken)
    {
        var versionInfo = context.GetRequiredVersionInfo();
        if (versionInfo.Downloads == null ||
            !versionInfo.Downloads.TryGetValue("client_mappings", out var mappings) ||
            string.IsNullOrWhiteSpace(mappings.Url))
        {
            throw new InvalidOperationException("client_mappings download information was not found in version json.");
        }

        if (installProfile.Data == null ||
            !installProfile.Data.TryGetValue("MOJMAPS", out var mojmapsEntry) ||
            string.IsNullOrWhiteSpace(mojmapsEntry.Client))
        {
            throw new InvalidOperationException("Forge install_profile does not contain data.MOJMAPS.client.");
        }

        var savePath = mojmapsEntry.Client!;
        if (IsWrapped(savePath, '[', ']'))
        {
            savePath = ForgeLibraryPathHelper.GetAbsoluteLibraryPath(context.Request.RootPath, savePath[1..^1]);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        await VanillaGameSubTaskFactory.DownloadSingleFileAsync(
            mappings.Url!,
            savePath,
            mappings.Sha1,
            mappings.Size,
            cancellationToken);

        _logger.Info($"Downloaded Forge MOJMAPS to '{savePath}'.");
    }

    private static bool IsWrapped(string value, char start, char end)
    {
        return value.Length >= 2 && value[0] == start && value[^1] == end;
    }

    private void TryTerminateProcess(Process process, string processorName)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException ex)
        {
            _logger.Warn($"Terminating Forge processor '{processorName}' is not supported: {ex.Message}");
        }
        catch (Win32Exception ex)
        {
            _logger.Warn($"Failed to terminate Forge processor '{processorName}': {ex.Message}");
        }
    }

    private static string EscapeArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        return argument.Contains(' ', StringComparison.Ordinal)
            ? $"\"{argument}\""
            : argument;
    }
}
