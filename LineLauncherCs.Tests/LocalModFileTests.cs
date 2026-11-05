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
using LMCCore.Game.Mods.Local;

namespace LineLauncherCs.Tests;

public class LocalModFileTests
{
    [Fact]
    public async Task Scan_ReadsMetadataAndKeepsDamagedFilesVisible()
    {
        using var scope = new TestFileSystemScope();
        var mods = scope.CreateDirectory("version/mods");
        WriteJar(Path.Combine(mods, "example.jar.disabled"), "fabric.mod.json",
            """{"id":"example","name":"Example Mod","version":"1.2.3"}""");
        File.WriteAllText(Path.Combine(mods, "broken.jar"), "not a jar");
        File.WriteAllText(Path.Combine(mods, "readme.txt"), "ignored");
        scope.CreateDirectory("version/mods/nested");
        WriteJar(Path.Combine(mods, "nested", "hidden.jar"), "fabric.mod.json", "{}");

        var files = await new LocalModManager(scope.GetPath("version")).ScanAsync();

        Assert.Equal(2, files.Count);
        var mod = Assert.Single(files, file => !file.HasMetadataError);
        Assert.Equal("Example Mod", mod.Name);
        Assert.Equal("1.2.3", mod.Version);
        Assert.Equal("Fabric", mod.Loader);
        Assert.False(mod.IsEnabled);
        Assert.Equal("broken", Assert.Single(files, file => file.HasMetadataError).Name);
    }

    [Fact]
    public async Task Enable_DoesNotOverwriteAnExistingFile()
    {
        using var scope = new TestFileSystemScope();
        var mods = scope.CreateDirectory("version/mods");
        var enabled = Path.Combine(mods, "example.jar");
        var disabled = enabled + ".disabled";
        File.WriteAllText(enabled, "existing");
        File.WriteAllText(disabled, "disabled");
        var manager = new LocalModManager(scope.GetPath("version"));
        var mod = new LocalModFile(disabled, "example", null, null, false, false);

        await Assert.ThrowsAsync<IOException>(() => manager.SetEnabledAsync(mod, true));

        Assert.Equal("existing", File.ReadAllText(enabled));
        Assert.Equal("disabled", File.ReadAllText(disabled));
    }

    [Fact]
    public async Task Import_DoesNotReplaceExistingModsOrLeaveTemporaryFiles()
    {
        using var scope = new TestFileSystemScope();
        var source = scope.GetPath("example.jar");
        File.WriteAllText(source, "source");
        var mods = scope.CreateDirectory("version/mods");
        var destination = Path.Combine(mods, "example.jar");
        File.WriteAllText(destination, "existing");

        await Assert.ThrowsAsync<IOException>(() =>
            new LocalModManager(scope.GetPath("version")).ImportAsync([source]));

        Assert.Equal("existing", File.ReadAllText(destination));
        Assert.Single(Directory.GetFiles(mods));
    }

    [Fact]
    public async Task Mutations_RejectFilesOutsideTheVersionModsDirectory()
    {
        using var scope = new TestFileSystemScope();
        var path = scope.GetPath("outside.jar");
        File.WriteAllText(path, "keep");
        var mod = new LocalModFile(path, "outside", null, null, true, false);
        var manager = new LocalModManager(scope.GetPath("version"));

        await Assert.ThrowsAsync<ArgumentException>(() => manager.DeleteAsync(mod));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.SetEnabledAsync(mod, false));
        Assert.Equal("keep", File.ReadAllText(path));
    }

    [Fact]
    public async Task EnableBatch_UpdatesAllTargetsAndLeavesOtherFilesAlone()
    {
        using var scope = new TestFileSystemScope();
        var mods = scope.CreateDirectory("version/mods");
        var first = Path.Combine(mods, "first.jar.disabled");
        var second = Path.Combine(mods, "second.jar.disabled");
        var other = Path.Combine(mods, "other.jar.disabled");
        File.WriteAllText(first, "first");
        File.WriteAllText(second, "second");
        File.WriteAllText(other, "keep");
        var manager = new LocalModManager(scope.GetPath("version"));

        var updated = await manager.SetEnabledAsync([
            new LocalModFile(first, "first", null, null, false, false),
            new LocalModFile(second, "second", null, null, false, false)
        ], true);

        Assert.All(updated, mod => Assert.True(mod.IsEnabled));
        Assert.Equal("first", File.ReadAllText(Path.Combine(mods, "first.jar")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(mods, "second.jar")));
        Assert.Equal("keep", File.ReadAllText(other));
    }

    [Fact]
    public async Task BatchMutations_ValidateAllPathsBeforeChangingAnyFile()
    {
        using var scope = new TestFileSystemScope();
        var mods = scope.CreateDirectory("version/mods");
        var inside = Path.Combine(mods, "inside.jar");
        var outside = scope.GetPath("outside.jar");
        File.WriteAllText(inside, "inside");
        File.WriteAllText(outside, "outside");
        var files = new[]
        {
            new LocalModFile(inside, "inside", null, null, true, false),
            new LocalModFile(outside, "outside", null, null, true, false)
        };
        var manager = new LocalModManager(scope.GetPath("version"));

        await Assert.ThrowsAsync<ArgumentException>(() => manager.SetEnabledAsync(files, false));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.DeleteAsync(files));
        Assert.Equal("inside", File.ReadAllText(inside));
        Assert.Equal("outside", File.ReadAllText(outside));
    }

    [Fact]
    public async Task Scan_HonorsCancellation()
    {
        using var scope = new TestFileSystemScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new LocalModManager(scope.RootPath).ScanAsync(cancellation.Token));
    }

    private static void WriteJar(string path, string entryName, string content)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
        writer.Write(content);
    }
}
