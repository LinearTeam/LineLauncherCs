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
    public string? TransformUrl(string officialUrl)
    {
        if (string.IsNullOrWhiteSpace(officialUrl))
            return null;

        var finalUrl = officialUrl;
        if (officialUrl.Contains("http://"))
        {
            finalUrl = officialUrl.Replace("http://", "https://");
        }
        
        // 尝试各种转换器
        var transformers = new[]
        {
            TransformVersionManifestUrl(finalUrl),
            TransformVersionJsonUrl(finalUrl),
            TransformAssetsUrl(finalUrl),
            TransformLibrariesUrl(finalUrl),
            TransformForgeUrl(finalUrl),
            TransformFabricMetaUrl(finalUrl),
            TransformFabricMavenUrl(finalUrl),
            TransformNeoForgeUrl(finalUrl),
            TransformLiteLoaderUrl(finalUrl),
            TransformAuthlibInjectorUrl(finalUrl),
            TransformMojangJavaUrl(finalUrl)
        };

        return transformers.OfType<string>().FirstOrDefault(officialUrl);

    }

    // 当前源转换失败时，自动向下一个源查找
    public string? TransformUrlWithFallback(string officialUrl)
    {
        var result = TransformUrl(officialUrl);
        return result ?? FallbackSource?.TransformUrlWithFallback(officialUrl);

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
}

// 官方源（Mojang官方）
public class OfficialDownloadSource : DownloadSource
{
    public OfficialDownloadSource()
    {
        Name = "Official";
    }

    public override string? TransformVersionManifestUrl(string officialUrl) =>
        officialUrl.StartsWith("https://launchermeta.mojang.com") ||
        officialUrl.StartsWith("https://launchermeta.mojang.com")
            ? officialUrl
            : null;

    public override string? TransformVersionJsonUrl(string officialUrl) =>
        officialUrl.StartsWith("https://launchermeta.mojang.com") ||
        officialUrl.StartsWith("https://launcher.mojang.com")
            ? officialUrl
            : null;

    public override string? TransformAssetsUrl(string officialUrl) =>
        officialUrl.StartsWith("https://resources.download.minecraft.net")
            ? officialUrl
            : null;

    public override string? TransformLibrariesUrl(string officialUrl) =>
        officialUrl.StartsWith("https://libraries.minecraft.net/")
            ? officialUrl
            : null;

    public override string? TransformForgeUrl(string officialUrl) =>
        officialUrl.StartsWith("https://files.minecraftforge.net/maven")
            ? officialUrl
            : null;

    public override string? TransformFabricMetaUrl(string officialUrl) =>
        officialUrl.StartsWith("https://meta.fabricmc.net")
            ? officialUrl
            : null;

    public override string? TransformFabricMavenUrl(string officialUrl) =>
        officialUrl.StartsWith("https://maven.fabricmc.net")
            ? officialUrl
            : null;

    public override string? TransformNeoForgeUrl(string officialUrl) =>
        officialUrl.StartsWith("https://maven.neoforged.net/releases")
            ? officialUrl
            : null;

    public override string? TransformLiteLoaderUrl(string officialUrl) =>
        officialUrl.StartsWith("https://dl.liteloader.com")
            ? officialUrl
            : null;

    public override string? TransformAuthlibInjectorUrl(string officialUrl) =>
        officialUrl.StartsWith("https://authlib-injector.yushi.moe")
            ? officialUrl
            : null;

    public override string? TransformMojangJavaUrl(string officialUrl) =>
        officialUrl.StartsWith("https://launchermeta.mojang.com/v1/products/java-runtime")
            ? officialUrl
            : null;
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
        // 替换 https://launchermeta.mojang.com/ 和 https://launcher.mojang.com/
        if (officialUrl.StartsWith("https://launchermeta.mojang.com/", StringComparison.OrdinalIgnoreCase))
            return officialUrl.Replace("https://launchermeta.mojang.com/", $"{BmclBase}/", StringComparison.OrdinalIgnoreCase);

        if (officialUrl.StartsWith("https://launcher.mojang.com/", StringComparison.OrdinalIgnoreCase))
            return officialUrl.Replace("https://launcher.mojang.com/", $"{BmclBase}/", StringComparison.OrdinalIgnoreCase);

        return null;
    }

    public override string? TransformAssetsUrl(string officialUrl)
    {
        // https://resources.download.minecraft.net -> https://bmclapi2.bangbang93.com/assets
        if (officialUrl.StartsWith("https://resources.download.minecraft.net", StringComparison.OrdinalIgnoreCase))
            return officialUrl.Replace("https://resources.download.minecraft.net", $"{BmclBase}/assets", StringComparison.OrdinalIgnoreCase);

        return null;
    }

    public override string? TransformLibrariesUrl(string officialUrl)
    {
        // https://libraries.minecraft.net/ -> https://bmclapi2.bangbang93.com/maven
        if (officialUrl.StartsWith("https://libraries.minecraft.net/", StringComparison.OrdinalIgnoreCase))
            return officialUrl.Replace("https://libraries.minecraft.net/", $"{BmclBase}/maven/", StringComparison.OrdinalIgnoreCase);

        return null;
    }

    public override string? TransformForgeUrl(string officialUrl)
    {
        // https://files.minecraftforge.net/maven -> https://bmclapi2.bangbang93.com/maven
        if (officialUrl.StartsWith("https://files.minecraftforge.net/maven", StringComparison.OrdinalIgnoreCase))
            return officialUrl.Replace("https://files.minecraftforge.net/maven", $"{BmclBase}/maven", StringComparison.OrdinalIgnoreCase);

        return null;
    }

    public override string? TransformFabricMetaUrl(string officialUrl)
    {
        // https://meta.fabricmc.net -> https://bmclapi2.bangbang93.com/fabric-meta
        if (officialUrl.StartsWith("https://meta.fabricmc.net", StringComparison.OrdinalIgnoreCase))
            return officialUrl.Replace("https://meta.fabricmc.net", $"{BmclBase}/fabric-meta", StringComparison.OrdinalIgnoreCase);

        return null;
    }

    public override string? TransformFabricMavenUrl(string officialUrl)
    {
        // https://maven.fabricmc.net -> https://bmclapi2.bangbang93.com/maven
        if (officialUrl.StartsWith("https://maven.fabricmc.net", StringComparison.OrdinalIgnoreCase))
            return officialUrl.Replace("https://maven.fabricmc.net", $"{BmclBase}/maven", StringComparison.OrdinalIgnoreCase);

        return null;
    }

    public override string? TransformNeoForgeUrl(string officialUrl)
    {
        // https://maven.neoforged.net/releases/net/neoforged/* -> https://bmclapi2.bangbang93.com/maven/net/neoforged/*
        if (officialUrl.StartsWith("https://maven.neoforged.net/releases/", StringComparison.OrdinalIgnoreCase))
            return officialUrl.Replace("https://maven.neoforged.net/releases/", $"{BmclBase}/maven/", StringComparison.OrdinalIgnoreCase);

        return null;
    }

    public override string? TransformLiteLoaderUrl(string officialUrl)
    {
        // https://dl.liteloader.com/versions/versions.json -> https://bmclapi.bangbang93.com/maven/com/mumfrey/liteloader/versions.json
        if (officialUrl.Equals("https://dl.liteloader.com/versions/versions.json", StringComparison.OrdinalIgnoreCase))
            return $"{BmclOldBase}/maven/com/mumfrey/liteloader/versions.json";

        // 其他LiteLoader资源
        return officialUrl.StartsWith("https://dl.liteloader.com/", StringComparison.OrdinalIgnoreCase) ? officialUrl.Replace("https://dl.liteloader.com/", $"{BmclBase}/maven/com/mumfrey/liteloader/", StringComparison.OrdinalIgnoreCase) : null;

    }

    public override string? TransformAuthlibInjectorUrl(string officialUrl)
    {
        // https://authlib-injector.yushi.moe -> https://bmclapi2.bangbang93.com/mirrors/authlib-injector
        return officialUrl.StartsWith("https://authlib-injector.yushi.moe", StringComparison.OrdinalIgnoreCase) ? officialUrl.Replace("https://authlib-injector.yushi.moe", $"{BmclBase}/mirrors/authlib-injector", StringComparison.OrdinalIgnoreCase) : null;

    }

    public override string? TransformMojangJavaUrl(string officialUrl)
    {
        // https://launchermeta.mojang.com/v1/products/java-runtime/... -> https://bmclapi2.bangbang93.com/v1/products/java-runtime/...
        return officialUrl.StartsWith("https://launchermeta.mojang.com/v1/products/java-runtime", StringComparison.OrdinalIgnoreCase) ? officialUrl.Replace("https://launchermeta.mojang.com/", $"{BmclBase}/", StringComparison.OrdinalIgnoreCase) : null;

    }
}

// 自定义
public class CustomDownloadSource : DownloadSource
{
    private readonly Dictionary<string, string> _urlMappings;

    public CustomDownloadSource(string name, Dictionary<string, string>? urlMappings)
    {
        Name = name;
        _urlMappings = urlMappings ?? new Dictionary<string, string>();
    }

    private string? TransformByMapping(string officialUrl)
    {
        foreach (var kvp in _urlMappings)
        {
            if (officialUrl.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return officialUrl.Replace(kvp.Key, kvp.Value, StringComparison.OrdinalIgnoreCase);
        }
        return null;
    }

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
