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
using LMCCore.Game.Modpacks;

namespace LineLauncherCs.Tests;

public sealed class ModpackLocalFileReuseTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Reuse_FindsExactPathInGameOrVersionDirectory(bool otherRoot, bool versionDirectory)
    {
        using var scope = new TestFileSystemScope();
        var current = scope.CreateDirectory("current");
        var other = scope.CreateDirectory("other");
        var sourceRoot = otherRoot ? other : current;
        if (versionDirectory)
            sourceRoot = Directory.CreateDirectory(Path.Combine(sourceRoot, "versions", "existing")).FullName;
        var source = Path.Combine(sourceRoot, "mods", "example.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        await File.WriteAllTextAsync(source, "example");
        var directories = await ModpackLocalFileReuse.GetSearchDirectoriesAsync(current,
            [current, other, other, scope.GetPath("missing"), "invalid\0root"], CancellationToken.None);
        Assert.Equal(current, directories[0]);
        Assert.Equal(directories.Count, directories.Distinct().Count());
        var target = scope.GetPath("cache/mods/example.jar");

        Assert.True(await ModpackLocalFileReuse.TryCopyAsync(CreateFile(), target, directories,
            CancellationToken.None));
        Assert.Equal("example", await File.ReadAllTextAsync(target));
        Assert.Equal("example", await File.ReadAllTextAsync(source));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(target)!));
    }

    [Theory]
    [InlineData("sha1")]
    [InlineData("sha512")]
    [InlineData("size")]
    [InlineData("path")]
    public async Task Reuse_RejectsMismatchAndPreservesDestination(string mismatch)
    {
        using var scope = new TestFileSystemScope();
        var root = scope.CreateDirectory("source");
        var sourceDirectory = scope.CreateDirectory("source/" + (mismatch == "path" ? "unrelated" : "mods"));
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "example.jar"),
            mismatch == "size" ? "wrong size" : "example");
        var file = CreateFile();
        if (mismatch is "sha1" or "sha512")
            file.Hashes[mismatch] = new string('0', file.Hashes[mismatch].Length);
        var cache = scope.CreateDirectory("cache");
        var target = Path.Combine(cache, "example.jar");
        await File.WriteAllTextAsync(target, "original");

        Assert.False(await ModpackLocalFileReuse.TryCopyAsync(file, target, [root], CancellationToken.None));
        Assert.Equal("original", await File.ReadAllTextAsync(target));
        Assert.Single(Directory.GetFiles(cache));
    }

    [Fact]
    public async Task Reuse_SkipsInvalidCandidateAndChecksLaterCandidate()
    {
        using var scope = new TestFileSystemScope();
        var bad = scope.CreateDirectory("bad");
        var good = scope.CreateDirectory("good");
        foreach (var root in new[] { bad, good })
        {
            Directory.CreateDirectory(Path.Combine(root, "mods"));
            await File.WriteAllTextAsync(Path.Combine(root, "mods", "example.jar"),
                root == bad ? "invalid" : "example");
        }
        var target = scope.GetPath("cache/example.jar");

        Assert.True(await ModpackLocalFileReuse.TryCopyAsync(CreateFile(), target, [bad, good],
            CancellationToken.None));
        Assert.Equal("example", await File.ReadAllTextAsync(target));
    }

    [Fact]
    public async Task Search_DoesNotRecurseIntoVersionContentsAndLimitsDirectories()
    {
        using var scope = new TestFileSystemScope();
        var root = scope.CreateDirectory("game");
        for (var i = 0; i < 140; i++)
            Directory.CreateDirectory(Path.Combine(root, "versions", $"v{i}", "nested"));

        var directories = await ModpackLocalFileReuse.GetSearchDirectoriesAsync(root, [], CancellationToken.None);

        Assert.InRange(directories.Count, 1, 128);
        Assert.DoesNotContain(directories, path => Path.GetFileName(path) == "nested");
    }

    [Fact]
    public async Task Reuse_LimitsHashCandidates()
    {
        using var scope = new TestFileSystemScope();
        var roots = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var root = scope.CreateDirectory($"source{i}");
            Directory.CreateDirectory(Path.Combine(root, "mods"));
            await File.WriteAllTextAsync(Path.Combine(root, "mods", "example.jar"),
                i == 4 ? "example" : "invalid");
            roots.Add(root);
        }
        var cache = scope.CreateDirectory("cache");

        Assert.False(await ModpackLocalFileReuse.TryCopyAsync(CreateFile(), Path.Combine(cache, "example.jar"),
            roots, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(cache));
    }

    [Fact]
    public async Task Reuse_PropagatesCancellationWithoutChangingDestination()
    {
        using var scope = new TestFileSystemScope();
        var target = scope.GetPath("example.jar");
        await File.WriteAllTextAsync(target, "original");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ModpackLocalFileReuse.TryCopyAsync(
            CreateFile(), target, [scope.RootPath], cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ModpackLocalFileReuse.GetSearchDirectoriesAsync(
            scope.RootPath, [], cancellation.Token));
        Assert.Equal("original", await File.ReadAllTextAsync(target));
        Assert.Single(Directory.GetFiles(scope.RootPath));
    }

    private static ModrinthModpackFile CreateFile()
    {
        var bytes = Encoding.UTF8.GetBytes("example");
        return new ModrinthModpackFile
        {
            Path = "mods/example.jar", FileSize = bytes.Length,
            Hashes = new()
            {
                ["sha1"] = Convert.ToHexString(SHA1.HashData(bytes)),
                ["sha512"] = Convert.ToHexString(SHA512.HashData(bytes))
            }
        };
    }
}
