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
using LMCCore.Game.Download;
using LMCCore.Game.Download.Model;

namespace LineLauncherCs.Tests;

public class DownloadSourceTests
{
    [Fact]
    public void TransformUrl_OfficialSourceKeepsRecognizedUrl()
    {
        var source = new OfficialDownloadSource();

        var result = source.TransformUrl("https://libraries.minecraft.net/com/example/lib.jar");

        Assert.Equal("https://libraries.minecraft.net/com/example/lib.jar", result);
    }

    [Fact]
    public void TransformUrl_BmclSourceTransformsKnownUrls()
    {
        var source = new BmclDownloadSource();

        var result = source.TransformUrl("https://resources.download.minecraft.net/ab/cdef");

        Assert.Equal("https://bmclapi2.bangbang93.com/assets/ab/cdef", result);
    }

    [Fact]
    public void TransformUrl_BmclSourceTransformsForgeMavenUrls()
    {
        var source = new BmclDownloadSource();

        var result = source.TransformUrl("https://maven.minecraftforge.net/net/minecraftforge/forge/demo/forge-demo-installer.jar");

        Assert.Equal("https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/demo/forge-demo-installer.jar", result);
    }

    [Fact]
    public void TransformUrl_NormalizesHttpBeforeTransforming()
    {
        var manager = DownloadSourceManager.CreateDefault();

        var result = manager.TransformUrl("http://resources.download.minecraft.net/ab/cdef");

        Assert.Equal("https://bmclapi2.bangbang93.com/assets/ab/cdef", result);
    }

    [Fact]
    public void TransformUrlWithFallback_UsesFallbackWhenPrimaryDoesNotMatch()
    {
        var primary = new CustomDownloadSource("custom", new Dictionary<string, string>
        {
            ["https://example.com/"] = "https://mirror.example.com/"
        })
        {
            FallbackSource = new OfficialDownloadSource()
        };

        var result = primary.TransformUrlWithFallback("https://libraries.minecraft.net/com/example/lib.jar");

        Assert.Equal("https://libraries.minecraft.net/com/example/lib.jar", result);
    }

    [Fact]
    public void TransformUrl_CustomSourceLeavesUnknownUrlUntouched()
    {
        var source = new CustomDownloadSource("custom", new Dictionary<string, string>
        {
            ["https://a.example/"] = "https://b.example/"
        });

        var result = source.TransformUrl("https://unknown.example/file");

        Assert.Equal("https://unknown.example/file", result);
    }

    [Fact]
    public void TransformUrl_UsesIndependentPoliciesForManifestAndFileDownloads()
    {
        var manager = DownloadSourceManager.CreateDefault(
            DownloadSourcePolicy.OfficialFirst,
            DownloadSourcePolicy.BmclapiFirst);

        var manifestUrl = manager.TransformUrl("https://launchermeta.mojang.com/mc/game/version_manifest.json");
        var fileUrl = manager.TransformUrl("https://libraries.minecraft.net/com/example/lib.jar");

        Assert.Equal("https://launchermeta.mojang.com/mc/game/version_manifest.json", manifestUrl);
        Assert.Equal("https://bmclapi2.bangbang93.com/maven/com/example/lib.jar", fileUrl);
    }

    [Fact]
    public void TransformUrl_CanPreferBmclForManifestAndOfficialForFiles()
    {
        var manager = DownloadSourceManager.CreateDefault(
            DownloadSourcePolicy.BmclapiFirst,
            DownloadSourcePolicy.OfficialFirst);

        var manifestUrl = manager.TransformUrl("https://launchermeta.mojang.com/mc/game/version_manifest.json");
        var fileUrl = manager.TransformUrl("https://libraries.minecraft.net/com/example/lib.jar");

        Assert.Equal("https://bmclapi2.bangbang93.com/mc/game/version_manifest.json", manifestUrl);
        Assert.Equal("https://libraries.minecraft.net/com/example/lib.jar", fileUrl);
    }

    [Fact]
    public void GetUrlCandidates_ReturnsPrimaryThenFallbackForFileDownloads()
    {
        var manager = DownloadSourceManager.CreateDefault(
            DownloadSourcePolicy.OfficialFirst,
            DownloadSourcePolicy.BmclapiFirst);

        var candidates = manager.GetUrlCandidates("https://libraries.minecraft.net/com/example/lib.jar");

        Assert.Equal(
        [
            "https://bmclapi2.bangbang93.com/maven/com/example/lib.jar",
            "https://libraries.minecraft.net/com/example/lib.jar"
        ], candidates);
    }

    [Fact]
    public void CreateDefault_UsesPoliciesFromCurrentConfig()
    {
        var previousConfig = Current.Config;
        try
        {
            Current.Config = new AppConfig
            {
                DefaultVersionManifestSource = DownloadSourcePolicy.OfficialFirst,
                DefaultFileDownloadSource = DownloadSourcePolicy.BmclapiFirst
            };

            var manager = DownloadSourceManager.CreateDefault();
            var manifestUrl = manager.TransformUrl("https://launchermeta.mojang.com/mc/game/version_manifest.json");
            var fileUrl = manager.TransformUrl("https://libraries.minecraft.net/com/example/lib.jar");

            Assert.Equal("https://launchermeta.mojang.com/mc/game/version_manifest.json", manifestUrl);
            Assert.Equal("https://bmclapi2.bangbang93.com/maven/com/example/lib.jar", fileUrl);
        }
        finally
        {
            Current.Config = previousConfig;
        }
    }
}
