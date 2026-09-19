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

using System.Security.Cryptography;
using System.Text;
using LMC;
using LMC.Basic.Configs;
using LMCCore.Game.Download.Installation.Caching;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Game.Versioning;

namespace LineLauncherCs.Tests;

public class GameInstallationLocalReuseHelperTests : IDisposable
{
    private readonly AppConfig _previousConfig = Current.Config;

    public GameInstallationLocalReuseHelperTests()
    {
        Current.Config = new AppConfig();
    }

    [Fact]
    public async Task TryPopulateClientJarFromKnownVersionsAsync_CopiesMatchingVersionJar()
    {
        using var scope = new TestFileSystemScope();
        var rootPath = scope.CreateDirectory(".minecraft");
        Current.Config.ManagedGameRootPaths = [rootPath];

        var sourceVersionDirectory = Path.Combine(rootPath, "versions", "1.20.6");
        Directory.CreateDirectory(sourceVersionDirectory);
        var sourceJarPath = Path.Combine(sourceVersionDirectory, "1.20.6.jar");
        await File.WriteAllTextAsync(sourceJarPath, "client-jar");

        var targetPath = scope.GetPath(@"cache\client.jar");
        var sourceBytes = await File.ReadAllBytesAsync(sourceJarPath);

        var reused = await GameInstallationLocalReuseHelper.TryPopulateClientJarFromKnownVersionsAsync(
            rootPath,
            targetPath,
            ComputeSha1(sourceBytes),
            sourceBytes.Length,
            CancellationToken.None);

        Assert.True(reused);
        Assert.True(File.Exists(targetPath));
        Assert.Equal("client-jar", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task TryPopulateOptiFineInstallerFromKnownVersionsAsync_CopiesMatchingVersionModFile()
    {
        using var scope = new TestFileSystemScope();
        var rootPath = scope.CreateDirectory(".minecraft");
        Current.Config.ManagedGameRootPaths = [rootPath];

        var sourceModsDirectory = Path.Combine(rootPath, "versions", "1.21.9-Fabric", "mods");
        Directory.CreateDirectory(sourceModsDirectory);
        var installerFileName = "OptiFine-1.21.9-HD_U-I6-installer.jar";
        var sourceInstallerPath = Path.Combine(sourceModsDirectory, installerFileName);
        await File.WriteAllTextAsync(sourceInstallerPath, "optifine-installer");

        var targetPath = scope.GetPath(@"cache\optifine-installer.jar");
        var reused = await GameInstallationLocalReuseHelper.TryPopulateOptiFineInstallerFromKnownVersionsAsync(
            rootPath,
            targetPath,
            installerFileName,
            CancellationToken.None);

        Assert.True(reused);
        Assert.True(File.Exists(targetPath));
        Assert.Equal("optifine-installer", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task WarmLibrariesFromManagedRootsAsync_CopiesMatchingLibraryFromOtherRoot()
    {
        using var scope = new TestFileSystemScope();
        var currentRoot = scope.CreateDirectory("current\\.minecraft");
        var otherRoot = scope.CreateDirectory("other\\.minecraft");
        Current.Config.ManagedGameRootPaths = [currentRoot, otherRoot];

        var relativePath = Path.Combine("com", "example", "demo", "1.0", "demo-1.0.jar");
        var sourcePath = Path.Combine(otherRoot, "libraries", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "library");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);

        await GameInstallationLocalReuseHelper.WarmLibrariesFromManagedRootsAsync(
            currentRoot,
            [
                new DownloadableFileInfo
                {
                    Path = relativePath.Replace(Path.DirectorySeparatorChar, '/'),
                    Sha1 = ComputeSha1(sourceBytes),
                    Size = sourceBytes.Length
                }
            ],
            CancellationToken.None);

        var targetPath = Path.Combine(currentRoot, "libraries", relativePath);
        Assert.True(File.Exists(targetPath));
        Assert.Equal("library", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task WarmAssetsFromManagedRootsAsync_CopiesMatchingAssetFromOtherRoot()
    {
        using var scope = new TestFileSystemScope();
        var currentRoot = scope.CreateDirectory("current\\.minecraft");
        var otherRoot = scope.CreateDirectory("other\\.minecraft");
        Current.Config.ManagedGameRootPaths = [currentRoot, otherRoot];

        var sourceBytes = Encoding.UTF8.GetBytes("asset");
        var hash = ComputeSha1(sourceBytes);
        var sourcePath = Path.Combine(otherRoot, "assets", "objects", hash[..2], hash);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllBytesAsync(sourcePath, sourceBytes);

        await GameInstallationLocalReuseHelper.WarmAssetsFromManagedRootsAsync(
            currentRoot,
            new Dictionary<string, AssetInfo>
            {
                ["demo"] = new()
                {
                    Hash = hash,
                    Size = sourceBytes.Length
                }
            },
            CancellationToken.None);

        var targetPath = Path.Combine(currentRoot, "assets", "objects", hash[..2], hash);
        Assert.True(File.Exists(targetPath));
        Assert.Equal("asset", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task TryPopulateAssetIndexFromManagedRootsAsync_CopiesMatchingAssetIndexFromOtherRoot()
    {
        using var scope = new TestFileSystemScope();
        var currentRoot = scope.CreateDirectory("current\\.minecraft");
        var otherRoot = scope.CreateDirectory("other\\.minecraft");
        Current.Config.ManagedGameRootPaths = [currentRoot, otherRoot];

        var targetPath = Path.Combine(currentRoot, "assets", "indexes", "1.21.json");
        var sourcePath = Path.Combine(otherRoot, "assets", "indexes", "1.21.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllTextAsync(sourcePath, "{}");
        var sourceBytes = await File.ReadAllBytesAsync(sourcePath);

        var reused = await GameInstallationLocalReuseHelper.TryPopulateAssetIndexFromManagedRootsAsync(
            currentRoot,
            new AssetIndexInfo
            {
                Id = "1.21",
                Sha1 = ComputeSha1(sourceBytes),
                Size = sourceBytes.Length
            },
            targetPath,
            CancellationToken.None);

        Assert.True(reused);
        Assert.True(File.Exists(targetPath));
        Assert.Equal("{}", await File.ReadAllTextAsync(targetPath));
    }

    public void Dispose()
    {
        Current.Config = _previousConfig;
    }

    private static string ComputeSha1(byte[] bytes)
    {
        return Convert.ToHexStringLower(SHA1.HashData(bytes));
    }
}
