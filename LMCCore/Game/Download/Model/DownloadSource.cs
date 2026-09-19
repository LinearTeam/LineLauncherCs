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

namespace LMCCore.Game.Download.Model;

public abstract class DownloadSource
{
    public string Name { get; protected set; } = string.Empty;

    public DownloadSource? FallbackSource { get; set; }

    // 转换版本信息URL
    // 如：https://launchermeta.mojang.com/mc/game/version_manifest.json
    public abstract string? TransformVersionManifestUrl(string officialUrl);


    // 转换版本JSON URL
    // 如：https://launchermeta.mojang.com/... -> 对应的源URL
    public abstract string? TransformVersionJsonUrl(string officialUrl);

    // 转换Assets URL
    // 如：https://resources.download.minecraft.net/... -> 对应的源URL
    public abstract string? TransformAssetsUrl(string officialUrl);

    // 转换Libraries URL
    // 如：https://libraries.minecraft.net/... -> 对应的源URL
    public abstract string? TransformLibrariesUrl(string officialUrl);

    // 转换Forge URL
    // 如：https://files.minecraftforge.net/maven/... -> 对应的源URL
    public abstract string? TransformForgeUrl(string officialUrl);

    // 转换Fabric元数据URL
    // 如：https://meta.fabricmc.net/... -> 对应的源URL
    public abstract string? TransformFabricMetaUrl(string officialUrl);

    // 转换Fabric Maven URL
    // 如：https://maven.fabricmc.net/... -> 对应的源URL
    public abstract string? TransformFabricMavenUrl(string officialUrl);

    // 转换NeoForge URL
    // 如：https://maven.neoforged.net/... -> 对应的源URL
    public abstract string? TransformNeoForgeUrl(string officialUrl);

    // 转换LiteLoader URL
    // 如：https://dl.liteloader.com/... -> 对应的源URL
    public abstract string? TransformLiteLoaderUrl(string officialUrl);

    // 转换authlib-injector URL
    // 如：https://authlib-injector.yushi.moe/... -> 对应的源URL
    public abstract string? TransformAuthlibInjectorUrl(string officialUrl);

    // 转换Mojang Java URL
    // 如：https://launchermeta.mojang.com/v1/products/java-runtime/... -> 对应的源URL
    public abstract string? TransformMojangJavaUrl(string officialUrl);

    // 通用URL转换方法，根据URL类型自动选择合适的转换器
    public string? TransformUrl(string? officialUrl)
    {
        var normalizedUrl = NormalizeUrl(officialUrl);
        return normalizedUrl == null
            ? null
            : TransformUrlCore(normalizedUrl) ?? normalizedUrl;
    }

    // 当前源转换失败时，自动向下一个源查找
    public string? TransformUrlWithFallback(string? officialUrl)
    {
        var normalizedUrl = NormalizeUrl(officialUrl);
        return normalizedUrl == null
            ? null
            : TransformUrlWithFallbackCore(normalizedUrl);
    }

    public IReadOnlyList<string> TransformUrlCandidates(string? officialUrl)
    {
        var normalizedUrl = NormalizeUrl(officialUrl);
        if (normalizedUrl == null)
        {
            return [];
        }

        var candidates = new List<string>();
        foreach (var source in GetSourceChain())
        {
            var transformedUrl = source.TransformUrlCore(normalizedUrl) ?? normalizedUrl;
            if (candidates.Contains(transformedUrl, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            candidates.Add(transformedUrl);
        }

        if (!candidates.Contains(normalizedUrl, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(normalizedUrl);
        }

        return candidates;
    }

    public IEnumerable<DownloadSource> GetSourceChain()
    {
        var current = this;
        while (current != null)
        {
            yield return current;
            current = current.FallbackSource;
        }
    }

    protected static string? KeepOriginalIfStartsWith(string officialUrl, params string[] prefixes)
    {
        return prefixes.Any(prefix => officialUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            ? officialUrl
            : null;
    }

    protected static string? ReplacePrefix(string officialUrl, string sourcePrefix, string targetPrefix)
    {
        return officialUrl.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)
            ? officialUrl.Replace(sourcePrefix, targetPrefix, StringComparison.OrdinalIgnoreCase)
            : null;
    }

    protected static string? ReplaceByMappings(string officialUrl, IReadOnlyDictionary<string, string> mappings)
    {
        foreach (var mapping in mappings)
        {
            var transformedUrl = ReplacePrefix(officialUrl, mapping.Key, mapping.Value);
            if (transformedUrl != null)
            {
                return transformedUrl;
            }
        }

        return null;
    }

    private string TransformUrlWithFallbackCore(string officialUrl)
    {
        return TransformUrlCore(officialUrl)
               ?? FallbackSource?.TransformUrlWithFallbackCore(officialUrl)
               ?? officialUrl;
    }

    private string? TransformUrlCore(string officialUrl)
    {
        return new[]
        {
            TransformVersionManifestUrl(officialUrl),
            TransformVersionJsonUrl(officialUrl),
            TransformAssetsUrl(officialUrl),
            TransformLibrariesUrl(officialUrl),
            TransformForgeUrl(officialUrl),
            TransformFabricMetaUrl(officialUrl),
            TransformFabricMavenUrl(officialUrl),
            TransformNeoForgeUrl(officialUrl),
            TransformLiteLoaderUrl(officialUrl),
            TransformAuthlibInjectorUrl(officialUrl),
            TransformMojangJavaUrl(officialUrl)
        }.FirstOrDefault(result => result != null);
    }

    private static string? NormalizeUrl(string? officialUrl)
    {
        if (string.IsNullOrWhiteSpace(officialUrl))
        {
            return null;
        }

        return officialUrl.Contains("http://", StringComparison.OrdinalIgnoreCase)
            ? officialUrl.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase)
            : officialUrl;
    }
}

// 官方源（Mojang官方）
public class OfficialDownloadSource : DownloadSource
{
    public const string ForgeMavenBaseUrl = "https://maven.minecraftforge.net";
    public const string ForgeLegacyFilesMavenBaseUrl = "https://files.minecraftforge.net/maven";

    public OfficialDownloadSource()
    {
        Name = "Official";
    }

    public override string? TransformVersionManifestUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, "https://launchermeta.mojang.com");

    public override string? TransformVersionJsonUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, "https://launchermeta.mojang.com", "https://launcher.mojang.com");

    public override string? TransformAssetsUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, "https://resources.download.minecraft.net");

    public override string? TransformLibrariesUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, "https://libraries.minecraft.net/");

    public override string? TransformForgeUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, ForgeMavenBaseUrl, ForgeLegacyFilesMavenBaseUrl);

    public override string? TransformFabricMetaUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, "https://meta.fabricmc.net");

    public override string? TransformFabricMavenUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, "https://maven.fabricmc.net");

    public override string? TransformNeoForgeUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, "https://maven.neoforged.net/releases");

    public override string? TransformLiteLoaderUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, "https://dl.liteloader.com");

    public override string? TransformAuthlibInjectorUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, "https://authlib-injector.yushi.moe");

    public override string? TransformMojangJavaUrl(string officialUrl) =>
        KeepOriginalIfStartsWith(officialUrl, "https://launchermeta.mojang.com/v1/products/java-runtime");
}

// <summary>
// BMCLAPI源（BangBang93 Mirror）
// 参考文档：https://bmclapi2.bangbang93.com
// </summary>
public class BmclDownloadSource : DownloadSource
{
    private const string BmclBase = "https://bmclapi2.bangbang93.com";
    private const string BmclOldBase = "https://bmclapi.bangbang93.com";

    public BmclDownloadSource()
    {
        Name = "BMCLAPI";
    }

    public override string? TransformVersionManifestUrl(string officialUrl)
    {
        return officialUrl switch
        {
            "https://launchermeta.mojang.com/mc/game/version_manifest.json" =>
                $"{BmclBase}/mc/game/version_manifest.json",
            "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json" =>
                $"{BmclBase}/mc/game/version_manifest_v2.json",
            _ => null
        };
    }

    public override string? TransformVersionJsonUrl(string officialUrl)
    {
        return ReplacePrefix(officialUrl, "https://launchermeta.mojang.com/", $"{BmclBase}/")
               ?? ReplacePrefix(officialUrl, "https://launcher.mojang.com/", $"{BmclBase}/");
    }

    public override string? TransformAssetsUrl(string officialUrl)
    {
        return ReplacePrefix(officialUrl, "https://resources.download.minecraft.net", $"{BmclBase}/assets");
    }

    public override string? TransformLibrariesUrl(string officialUrl)
    {
        return ReplacePrefix(officialUrl, "https://libraries.minecraft.net/", $"{BmclBase}/maven/");
    }

    public override string? TransformForgeUrl(string officialUrl) =>
        ReplacePrefix(officialUrl, OfficialDownloadSource.ForgeMavenBaseUrl, $"{BmclBase}/maven")
        ?? ReplacePrefix(officialUrl, OfficialDownloadSource.ForgeLegacyFilesMavenBaseUrl, $"{BmclBase}/maven");

    public override string? TransformFabricMetaUrl(string officialUrl)
    {
        return ReplacePrefix(officialUrl, "https://meta.fabricmc.net", $"{BmclBase}/fabric-meta");
    }

    public override string? TransformFabricMavenUrl(string officialUrl)
    {
        return ReplacePrefix(officialUrl, "https://maven.fabricmc.net", $"{BmclBase}/maven");
    }

    public override string? TransformNeoForgeUrl(string officialUrl)
    {
        return ReplacePrefix(officialUrl, "https://maven.neoforged.net/releases/", $"{BmclBase}/maven/");
    }

    public override string? TransformLiteLoaderUrl(string officialUrl)
    {
        if (officialUrl.Equals("https://dl.liteloader.com/versions/versions.json", StringComparison.OrdinalIgnoreCase))
            return $"{BmclOldBase}/maven/com/mumfrey/liteloader/versions.json";

        return ReplacePrefix(officialUrl, "https://dl.liteloader.com/", $"{BmclBase}/maven/com/mumfrey/liteloader/");
    }

    public override string? TransformAuthlibInjectorUrl(string officialUrl)
    {
        return ReplacePrefix(officialUrl, "https://authlib-injector.yushi.moe", $"{BmclBase}/mirrors/authlib-injector");
    }

    public override string? TransformMojangJavaUrl(string officialUrl)
    {
        return ReplacePrefix(officialUrl, "https://launchermeta.mojang.com/v1/products/java-runtime", $"{BmclBase}/v1/products/java-runtime");
    }
}

// 自定义
public class CustomDownloadSource : DownloadSource
{
    private readonly IReadOnlyDictionary<string, string> _urlMappings;

    public CustomDownloadSource(string name, Dictionary<string, string>? urlMappings)
    {
        Name = name;
        _urlMappings = urlMappings ?? new Dictionary<string, string>();
    }

    private string? TransformByMapping(string officialUrl) => ReplaceByMappings(officialUrl, _urlMappings);

    public override string? TransformVersionManifestUrl(string officialUrl) => TransformByMapping(officialUrl);
    public override string? TransformVersionJsonUrl(string officialUrl) => TransformByMapping(officialUrl);
    public override string? TransformAssetsUrl(string officialUrl) => TransformByMapping(officialUrl);
    public override string? TransformLibrariesUrl(string officialUrl) => TransformByMapping(officialUrl);
    public override string? TransformForgeUrl(string officialUrl) => TransformByMapping(officialUrl);
    public override string? TransformFabricMetaUrl(string officialUrl) => TransformByMapping(officialUrl);
    public override string? TransformFabricMavenUrl(string officialUrl) => TransformByMapping(officialUrl);
    public override string? TransformNeoForgeUrl(string officialUrl) => TransformByMapping(officialUrl);
    public override string? TransformLiteLoaderUrl(string officialUrl) => TransformByMapping(officialUrl);
    public override string? TransformAuthlibInjectorUrl(string officialUrl) => TransformByMapping(officialUrl);
    public override string? TransformMojangJavaUrl(string officialUrl) => TransformByMapping(officialUrl);
}
