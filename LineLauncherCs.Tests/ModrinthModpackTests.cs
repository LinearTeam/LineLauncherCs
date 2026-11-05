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
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LMCCore.Game.Modpacks;

namespace LineLauncherCs.Tests;

public sealed class ModrinthModpackTests
{
    [Theory]
    [InlineData(".zip")]
    [InlineData(".mrpack")]
    public async Task Read_UsesRootIndexRatherThanExtension(string extension)
    {
        using var scope = new TestFileSystemScope();
        var path = scope.GetPath("pack" + extension);
        WriteArchive(path, CreateIndex());
        var pack = await ModrinthModpackReader.ReadAsync(path);
        Assert.Equal("Example", pack.Index.Name);
        Assert.Equal("1.20.1", pack.MinecraftVersion);
        Assert.Null(pack.LoaderId);
        Assert.Equal(64, pack.ArchiveSha256.Length);
    }

    [Fact]
    public async Task Read_RejectsAnOrdinaryZipAndDuplicateJsonProperties()
    {
        using var scope = new TestFileSystemScope();
        var path = scope.GetPath("ordinary.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            archive.CreateEntry("readme.txt");
        await Assert.ThrowsAsync<InvalidDataException>(() => ModrinthModpackReader.ReadAsync(path));
        var duplicate = scope.GetPath("duplicate.zip");
        WriteArchive(duplicate, """{"formatVersion":1,"game":"minecraft","name":"a","name":"b","versionId":"1","files":[],"dependencies":{"minecraft":"1.20.1"}}""");
        await Assert.ThrowsAsync<InvalidDataException>(() => ModrinthModpackReader.ReadAsync(duplicate));
    }

    [Theory]
    [InlineData("neoforge")]
    [InlineData("quilt-loader")]
    [InlineData("unknown-loader")]
    public async Task Read_RejectsUnsupportedDependencies(string dependency)
    {
        using var scope = new TestFileSystemScope();
        var path = scope.GetPath("pack.zip");
        WriteArchive(path, CreateIndex(dependencies: new() { ["minecraft"] = "1.20.1", [dependency] = "1.0" }));
        await Assert.ThrowsAsync<NotSupportedException>(() => ModrinthModpackReader.ReadAsync(path));
    }

    [Fact]
    public async Task Read_RejectsConflictingLoadersAndMissingRequiredFields()
    {
        using var scope = new TestFileSystemScope();
        var conflicting = scope.GetPath("conflict.zip");
        WriteArchive(conflicting, CreateIndex(dependencies: new()
        {
            ["minecraft"] = "1.20.1", ["fabric-loader"] = "0.16.0", ["forge"] = "47.2.0"
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => ModrinthModpackReader.ReadAsync(conflicting));
        var missing = scope.GetPath("missing.zip");
        WriteArchive(missing, """{"formatVersion":1,"game":"minecraft","name":"Example","versionId":"1"}""");
        await Assert.ThrowsAsync<JsonException>(() => ModrinthModpackReader.ReadAsync(missing));
    }

    [Theory]
    [InlineData("../outside.jar")]
    [InlineData("/mods/a.jar")]
    [InlineData("C:/mods/a.jar")]
    [InlineData("mods\\a.jar")]
    [InlineData("mods/CON.jar")]
    [InlineData("mods/COM\u00b9.jar")]
    [InlineData("mods/a.jar.")]
    [InlineData("mods//a.jar")]
    public void Paths_RejectUnsafeOrAmbiguousPaths(string path) =>
        Assert.Throws<InvalidDataException>(() => ModpackPaths.Normalize(path));

    [Fact]
    public async Task Read_RejectsZipTraversalLinksAndDuplicatePaths()
    {
        using var scope = new TestFileSystemScope();
        var traversal = scope.GetPath("traversal.zip");
        WriteArchive(traversal, CreateIndex(), ("overrides/../../outside.txt", "bad"));
        await Assert.ThrowsAsync<InvalidDataException>(() => ModrinthModpackReader.ReadAsync(traversal));
        var duplicate = scope.GetPath("duplicate.zip");
        WriteArchive(duplicate, CreateIndex(), ("overrides/a", "1"), ("overrides/A", "2"));
        await Assert.ThrowsAsync<InvalidDataException>(() => ModrinthModpackReader.ReadAsync(duplicate));
        var link = scope.GetPath("link.zip");
        WriteArchive(link, CreateIndex());
        using (var archive = ZipFile.Open(link, ZipArchiveMode.Update))
            archive.CreateEntry("overrides/link").ExternalAttributes = 0xA1FF << 16;
        await Assert.ThrowsAsync<InvalidDataException>(() => ModrinthModpackReader.ReadAsync(link));
    }

    [Fact]
    public void Paths_ProtectVersionFilesAndRejectFileDirectoryCollisions()
    {
        Assert.Throws<InvalidDataException>(() => ModpackPaths.EnsureNotReserved("Example.json", "Example"));
        Assert.Throws<InvalidDataException>(() => ModpackPaths.EnsureNotReserved("LMC/version_config.json", "Example"));
        Assert.Throws<InvalidDataException>(() =>
            ModrinthModpackInstaller.ValidateOutputPaths(["mods", "mods/example.jar"]));
        Assert.Throws<InvalidDataException>(() =>
            ModrinthModpackInstaller.ValidateOutputPaths(["mods/A.jar", "mods/a.jar"]));
        ModrinthModpackInstaller.ValidateOutputPaths(["config/a.txt", "config/a.txt"]);
        Assert.Equal("mods/caf\u00e9.jar", ModpackPaths.Normalize("mods/cafe\u0301.jar"));
        Assert.Throws<InvalidDataException>(() => ModpackPaths.EnsureNotReserved("caf\u00e9.json", "cafe\u0301"));
    }

    [Fact]
    public void Selection_InstallsRequiredAndSelectedOptionalClientFilesOnly()
    {
        var index = new ModrinthModpackIndex
        {
            Files =
            [
                new() { Path = "required.jar" },
                new() { Path = "selected.jar", Env = new() { Client = "optional" } },
                new() { Path = "unselected.jar", Env = new() { Client = "optional" } },
                new() { Path = "server.jar", Env = new() { Client = "unsupported" } }
            ]
        };
        var pack = new ModrinthModpackInfo("", "", index, "1.20.1", null, null);
        var installation = new ModrinthModpackInstallation(pack, new HashSet<string> { "selected.jar" });
        Assert.Equal(["required.jar", "selected.jar"], installation.GetSelectedFiles().Select(file => file.Path));
    }

    [Fact]
    public async Task Overrides_ApplyClientLayerAfterCommonLayerAndIgnoreServerLayer()
    {
        using var scope = new TestFileSystemScope();
        var source = scope.GetPath("source.zip");
        WriteArchive(source, CreateIndex(),
            ("overrides/config/example.txt", "common"),
            ("client-overrides/config/example.txt", "client"),
            ("server-overrides/config/example.txt", "server"),
            ("overrides/options.txt", "options"));
        var content = scope.CreateDirectory("content");
        await ModrinthModpackInstaller.ExtractOverridesAsync(source, content, "Example", CancellationToken.None);
        Assert.Equal("client", await File.ReadAllTextAsync(Path.Combine(content, "config/example.txt")));
        Assert.Equal("options", await File.ReadAllTextAsync(Path.Combine(content, "options.txt")));
    }

    [Fact]
    public async Task Prepare_RejectsArchiveChangedAfterInspection()
    {
        using var scope = new TestFileSystemScope();
        var source = scope.GetPath("source.zip");
        WriteArchive(source, CreateIndex());
        var pack = await ModrinthModpackReader.ReadAsync(source);
        using (var archive = ZipFile.Open(source, ZipArchiveMode.Update))
            archive.CreateEntry("overrides/new.txt");
        var installation = new ModrinthModpackInstallation(pack, new HashSet<string>());
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ModrinthModpackInstaller.PrepareArchiveAsync(installation, scope.GetPath("cache/pack.zip"),
                scope.GetPath("cache/content"), CancellationToken.None));
    }

    [Fact]
    public async Task FileValidation_RequiresBothHashesAndSize()
    {
        using var scope = new TestFileSystemScope();
        var bytes = Encoding.UTF8.GetBytes("example");
        var path = scope.GetPath("file.jar");
        await File.WriteAllBytesAsync(path, bytes);
        var file = new ModrinthModpackFile
        {
            Path = "mods/file.jar", FileSize = bytes.Length,
            Hashes = new()
            {
                ["sha1"] = Convert.ToHexString(SHA1.HashData(bytes)),
                ["sha512"] = Convert.ToHexString(SHA512.HashData(bytes))
            }
        };
        await ModpackFileDownloader.ValidateFileAsync(path, file, CancellationToken.None);
        file.Hashes["sha512"] = new string('0', 128);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            ModpackFileDownloader.ValidateFileAsync(path, file, CancellationToken.None));
    }

    [Fact]
    public async Task Read_RequiresCompleteFileHashesAndUniqueNormalizedPaths()
    {
        using var scope = new TestFileSystemScope();
        var file = new ModrinthModpackFile
        {
            Path = "mods/example.jar", FileSize = 1,
            Downloads = ["https://cdn.modrinth.com/data/example.jar"],
            Hashes = new() { ["sha1"] = new string('a', 40), ["sha512"] = new string('b', 128) }
        };
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var index = new ModrinthModpackIndex
        {
            FormatVersion = 1, Game = "minecraft", Name = "Example", VersionId = "1",
            Dependencies = new() { ["minecraft"] = "1.20.1", ["fabric-loader"] = "0.16.0" },
            Files = [file]
        };
        var valid = scope.GetPath("valid.zip");
        WriteArchive(valid, JsonSerializer.Serialize(index, options));
        var pack = await ModrinthModpackReader.ReadAsync(valid);
        Assert.Equal("fabric-loader", pack.LoaderId);
        Assert.Equal("0.16.0", pack.LoaderVersion);
        Assert.Equal("mods/example.jar", Assert.Single(pack.Index.Files).Path);

        file.Hashes.Remove("sha512");
        var invalid = scope.GetPath("invalid.zip");
        WriteArchive(invalid, JsonSerializer.Serialize(index, options));
        await Assert.ThrowsAsync<InvalidDataException>(() => ModrinthModpackReader.ReadAsync(invalid));

        file.Hashes["sha512"] = new string('b', 128);
        index.Files.Add(file);
        var duplicate = scope.GetPath("duplicate.zip");
        WriteArchive(duplicate, JsonSerializer.Serialize(index, options));
        await Assert.ThrowsAsync<InvalidDataException>(() => ModrinthModpackReader.ReadAsync(duplicate));
    }

    [Theory]
    [InlineData("http://cdn.modrinth.com/a.jar")]
    [InlineData("https://localhost/a.jar")]
    [InlineData("https://cdn.modrinth.com.evil.example/a.jar")]
    [InlineData("https://user@cdn.modrinth.com/a.jar")]
    [InlineData("https://cdn.modrinth.com:444/a.jar")]
    public void DownloadUrls_RejectUntrustedDestinations(string url) =>
        Assert.Throws<InvalidDataException>(() => ModpackFileDownloader.ValidateUrl(url, false));

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("169.254.169.254")]
    [InlineData("192.168.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fc00::1")]
    public void DownloadAddresses_RejectLocalNetworks(string address) =>
        Assert.False(ModpackFileDownloader.IsPublicAddress(IPAddress.Parse(address)));

    private static string CreateIndex(Dictionary<string, string>? dependencies = null) =>
        JsonSerializer.Serialize(new
        {
            formatVersion = 1, game = "minecraft", name = "Example", versionId = "1.0",
            files = Array.Empty<object>(),
            dependencies = dependencies ?? new() { ["minecraft"] = "1.20.1" }
        });

    private static void WriteArchive(string path, string index, params (string Path, string Content)[] entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(archive.CreateEntry("modrinth.index.json").Open()))
            writer.Write(index);
        foreach (var entry in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entry.Path).Open());
            writer.Write(entry.Content);
        }
    }
}
