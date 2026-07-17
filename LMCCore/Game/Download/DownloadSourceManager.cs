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

using LMC;
using LMC.Basic.Configs;
using LMCCore.Game.Download.Model;

namespace LMCCore.Game.Download;

public class DownloadSourceManager
{
    private readonly DownloadSource _manifestSource;
    private readonly DownloadSource _fileSource;

    private DownloadSourceManager(DownloadSource manifestSource, DownloadSource fileSource)
    {
        _manifestSource = manifestSource ?? throw new ArgumentNullException(nameof(manifestSource));
        _fileSource = fileSource ?? throw new ArgumentNullException(nameof(fileSource));
    }

    public static DownloadSourceManager CreateDefault()
    {
        var config = Current.Config;
        var manifestPolicy = config?.DefaultVersionManifestSource ?? DownloadSourcePolicy.BmclapiFirst;
        var filePolicy = config?.DefaultFileDownloadSource ?? DownloadSourcePolicy.BmclapiFirst;
        return CreateDefault(manifestPolicy, filePolicy);
    }

    public static DownloadSourceManager CreateDefault(
        DownloadSourcePolicy manifestPolicy,
        DownloadSourcePolicy filePolicy)
    {
        return new DownloadSourceManager(
            CreateSourceChain(manifestPolicy),
            CreateSourceChain(filePolicy));
    }

    public string? TransformUrl(string? officialUrl)
    {
        return SelectSource(officialUrl).TransformUrlWithFallback(officialUrl);
    }

    public IReadOnlyList<string> GetUrlCandidates(string? officialUrl)
    {
        return SelectSource(officialUrl).TransformUrlCandidates(officialUrl);
    }

    public IEnumerable<string> GetSourceChainInfo()
    {
        return _manifestSource.GetSourceChain()
            .Concat(_fileSource.GetSourceChain())
            .Select(source => source.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private DownloadSource SelectSource(string? officialUrl)
    {
        return IsVersionManifestUrl(officialUrl) ? _manifestSource : _fileSource;
    }

    private static DownloadSource CreateSourceChain(DownloadSourcePolicy policy)
    {
        return policy switch
        {
            DownloadSourcePolicy.OfficialFirst => new OfficialDownloadSource
            {
                FallbackSource = new BmclDownloadSource()
            },
            _ => new BmclDownloadSource
            {
                FallbackSource = new OfficialDownloadSource()
            }
        };
    }

    private static bool IsVersionManifestUrl(string? officialUrl)
    {
        if (string.IsNullOrWhiteSpace(officialUrl))
        {
            return false;
        }

        return officialUrl.StartsWith("https://launchermeta.mojang.com/mc/game/version_manifest", StringComparison.OrdinalIgnoreCase) ||
               officialUrl.StartsWith("http://launchermeta.mojang.com/mc/game/version_manifest", StringComparison.OrdinalIgnoreCase);
    }
}
