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
using LMC.Basic.Logging;
using LMCCore.Game.Download.Model.Vanilla;
using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.LocalVersion.Arguments;

namespace LMCCore.Game.Versioning;

internal sealed class VersionClientVersionResolver(
    Func<CancellationToken, Task<VersionManifestInfo>> manifestLoader,
    Logger logger)
{
    private readonly Func<CancellationToken, Task<VersionManifestInfo>> _manifestLoader = manifestLoader;
    private readonly Logger _logger = logger;

    public async Task<string> ResolveAsync(LocalVersionInfo versionInfo, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(versionInfo.ClientVersion))
        {
            return versionInfo.ClientVersion;
        }

        if (TryResolveFromPatches(versionInfo, out var patchVersion))
        {
            return patchVersion;
        }

        if (TryResolveFromGameArguments(versionInfo, out var gameArgumentVersion))
        {
            return gameArgumentVersion;
        }

        if (TryResolveFromLibraries(versionInfo, out var libraryVersion))
        {
            return libraryVersion;
        }

        if (versionInfo.ReleaseTime == null)
        {
            _logger.Warn($"版本 {versionInfo.Id} 无法识别");
            return LocalGameVersionEntry.UnknownClientVersionId;
        }

        try
        {
            var manifest = await _manifestLoader(cancellationToken);
            var matchedVersion = manifest.Versions
                .Where(entry => entry.ReleaseTime == versionInfo.ReleaseTime.Value && entry.ReleaseTime.Year > 2009)
                .OrderByDescending(entry => string.Equals(entry.Type, "release", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (matchedVersion == null)
            {
                _logger.Warn($"版本 {versionInfo.Id} 的 releaseTime ({versionInfo.ReleaseTime}) 未在版本清单中匹配到任何条目，无法识别客户端版本");
            }

            return matchedVersion?.Id ?? LocalGameVersionEntry.UnknownClientVersionId;
        }
        catch (Exception ex)
        {
            _logger.Warn($"根据 releaseTime 识别本地版本失败: {ex.Message}");
            return LocalGameVersionEntry.UnknownClientVersionId;
        }
    }

    internal static bool TryResolveFromPatches(LocalVersionInfo versionInfo, out string clientVersionId)
    {
        clientVersionId = string.Empty;
        if (!(versionInfo.Patches?.Any(patch => patch.Id == "game") ?? false))
        {
            return false;
        }

        clientVersionId = versionInfo.Patches.First(patch => patch.Id == "game").Version ?? string.Empty;
        return !string.IsNullOrWhiteSpace(clientVersionId);
    }

    internal static bool TryResolveFromGameArguments(LocalVersionInfo versionInfo, out string clientVersionId)
    {
        clientVersionId = string.Empty;
        if (versionInfo.Arguments == null ||
            !versionInfo.Arguments.TryGetValue("game", out var gameArgs) ||
            gameArgs is not { Count: > 0 })
        {
            return false;
        }

        var index = gameArgs.FindIndex(arg => arg is StringGameArgument sga &&
                                              sga.Value.Trim().Equals("--fml.mcVersion", StringComparison.Ordinal));
        if (index < 0 || gameArgs.Count <= index + 1 || gameArgs[index + 1] is not StringGameArgument nextValue)
        {
            return false;
        }

        clientVersionId = nextValue.Value;
        return !string.IsNullOrWhiteSpace(clientVersionId);
    }

    internal static bool TryResolveFromLibraries(LocalVersionInfo versionInfo, out string clientVersionId)
    {
        clientVersionId = string.Empty;

        return TryExtractVersionFromLibrary(versionInfo, "net.minecraftforge:fmlloader:", '-', out clientVersionId)
               || TryExtractVersionFromLibrary(versionInfo, "net.minecraftforge:forge:", '-', out clientVersionId)
               || TryExtractVersionFromLibrary(versionInfo, "optifine:OptiFine:", '_', out clientVersionId)
               || TryExtractVersionFromLibrary(versionInfo, "net.fabricmc:intermediary:", null, out clientVersionId);
    }

    internal static bool TryExtractVersionFromLibrary(LocalVersionInfo versionInfo, string prefix, char? separator, out string clientVersionId)
    {
        clientVersionId = string.Empty;
        var library = versionInfo.Libraries.Find(item => item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (library == null)
        {
            return false;
        }

        var versionText = library.Name.Trim().Replace(prefix, string.Empty, StringComparison.OrdinalIgnoreCase);
        if (separator.HasValue)
        {
            var separatorIndex = versionText.IndexOf(separator.Value);
            if (separatorIndex > 0)
            {
                versionText = versionText[..separatorIndex];
            }
        }

        if (string.IsNullOrWhiteSpace(versionText))
        {
            return false;
        }

        clientVersionId = versionText;
        return true;
    }
}
