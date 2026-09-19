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

using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Model.LocalVersion.Libraries;
using LMCCore.Game.Versioning.Discovery;

namespace LineLauncherCs.Tests;

public class LocalGameModLoaderResolverTests
{
    [Fact]
    public void Resolve_FabricAndModernForge_ReturnsLoaderVersions()
    {
        var versionInfo = CreateVersionInfo(
            "1.21.11",
            "net.fabricmc:fabric-loader:0.19.3",
            "net.minecraftforge:forge:1.21.11-61.1.8:universal");

        var loaders = LocalGameModLoaderResolver.Resolve(versionInfo, "1.21.11");

        Assert.Collection(
            loaders,
            fabric =>
            {
                Assert.Equal(LocalGameModLoaderType.Fabric, fabric.Type);
                Assert.Equal("0.19.3", fabric.VersionId);
            },
            forge =>
            {
                Assert.Equal(LocalGameModLoaderType.Forge, forge.Type);
                Assert.Equal("61.1.8", forge.VersionId);
            });
    }

    [Fact]
    public void Resolve_LegacyForge_ReturnsForgeVersionWithoutMinecraftVersion()
    {
        var versionInfo = CreateVersionInfo(
            "1.8.9",
            "net.minecraftforge:forge:1.8.9-11.15.1.2318-1.8.9");

        var loader = Assert.Single(LocalGameModLoaderResolver.Resolve(versionInfo, "1.8.9"));

        Assert.Equal(LocalGameModLoaderType.Forge, loader.Type);
        Assert.Equal("11.15.1.2318", loader.VersionId);
    }

    [Fact]
    public void Resolve_OptiFine_ReturnsPatchIdentifier()
    {
        var versionInfo = CreateVersionInfo(
            "1.12.2",
            "optifine:OptiFine:1.12.2_HD_U_G5");

        var loader = Assert.Single(LocalGameModLoaderResolver.Resolve(versionInfo, "1.12.2"));

        Assert.Equal(LocalGameModLoaderType.OptiFine, loader.Type);
        Assert.Equal("HD_U_G5", loader.VersionId);
    }

    private static LocalVersionInfo CreateVersionInfo(string id, params string[] libraries)
    {
        return new LocalVersionInfo
        {
            Id = id,
            MainClass = "main",
            Libraries = libraries
                .Select(name => (ILibraryInfo)new LibraryInfo { Name = name })
                .ToList()
        };
    }
}
