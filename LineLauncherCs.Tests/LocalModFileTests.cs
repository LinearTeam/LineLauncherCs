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
    public async Task Import_ConflictCanReplaceDisabledModWithoutEnablingIt()
    {
        using var scope = new TestFileSystemScope();
        var source = scope.GetPath("example.jar");
        await File.WriteAllTextAsync(source, "replacement");
        var mods = scope.CreateDirectory("version/mods");
        var disabledPath = Path.Combine(mods, "example.jar.disabled");
        await File.WriteAllTextAsync(disabledPath, "old");
        var manager = new LocalModManager(scope.GetPath("version"));

        var result = await manager.ImportAsync([source], (_, _) =>
            Task.FromResult(new ModImportChoice(ModImportDecision.Replace)));

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal("replacement", await File.ReadAllTextAsync(disabledPath));
        Assert.False(File.Exists(Path.Combine(mods, "example.jar")));
    }

    [Fact]
    public async Task Import_AmbiguousExistingStateReportsErrorWithoutCallbackOrMutation()
    {
        using var scope = new TestFileSystemScope();
        var source = scope.GetPath("example.jar");
        await File.WriteAllTextAsync(source, "replacement");
        var mods = scope.CreateDirectory("version/mods");
        await File.WriteAllTextAsync(Path.Combine(mods, "example.jar"), "enabled");
        await File.WriteAllTextAsync(Path.Combine(mods, "example.jar.disabled"), "disabled");
        var prompted = false;

        var result = await new LocalModManager(scope.GetPath("version")).ImportAsync([source], (_, _) =>
        {
            prompted = true;
            return Task.FromResult(new ModImportChoice(ModImportDecision.Replace));
        });

        Assert.False(prompted);
        Assert.Equal(0, result.SkippedCount);
        Assert.Contains("enabled and disabled", Assert.Single(result.Errors));
        Assert.Equal("enabled", await File.ReadAllTextAsync(Path.Combine(mods, "example.jar")));
        Assert.Equal("disabled", await File.ReadAllTextAsync(Path.Combine(mods, "example.jar.disabled")));
    }

    [Fact]
    public async Task Import_RejectsMissingVersionDirectoryAndReparsePoint()
    {
        using var scope = new TestFileSystemScope();
        var source = scope.GetPath("example.jar");
        await File.WriteAllTextAsync(source, "jar");
        var deletedVersion = scope.GetPath("deleted-version");
        var manager = new LocalModManager(deletedVersion);
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => manager.ImportAsync([source]));
        Assert.False(Directory.Exists(deletedVersion));

        var version = scope.CreateDirectory("version");
        var link = Path.Combine(scope.RootPath, "linked.jar");
        try
        {
            File.CreateSymbolicLink(link, source);
            var result = await new LocalModManager(version).ImportAsync([link], (_, _) =>
                Task.FromResult(new ModImportChoice(ModImportDecision.Skip)));
            Assert.Single(result.Errors);
            Assert.Empty(Directory.GetFiles(Path.Combine(version, "mods")));
        }
        catch (PlatformNotSupportedException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public async Task Import_DeduplicatesNormalizedSourcesAndSkipsSelfCopy()
    {
        using var scope = new TestFileSystemScope();
        var version = scope.CreateDirectory("version");
        var source = scope.GetPath("example.jar");
        await File.WriteAllTextAsync(source, "source");
        var prompts = 0;
        var manager = new LocalModManager(version);

        var result = await manager.ImportAsync(
            [source, Path.Combine(scope.RootPath, ".", "example.jar"), source], (_, _) =>
            {
                prompts++;
                return Task.FromResult(new ModImportChoice(ModImportDecision.Replace));
            });
        await manager.ImportAsync([Path.Combine(manager.ModsDirectory, "example.jar")]);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(2, result.SkippedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(0, prompts);
        Assert.Single(Directory.GetFiles(manager.ModsDirectory));
    }

    [Fact]
    public async Task Import_DifferentSourcesWithSameTargetNameAskBeforeReplacing()
    {
        using var scope = new TestFileSystemScope();
        var first = Path.Combine(scope.CreateDirectory("first"), "same.jar");
        var second = Path.Combine(scope.CreateDirectory("second"), "same.jar");
        await File.WriteAllTextAsync(first, "first");
        await File.WriteAllTextAsync(second, "second");
        var version = scope.CreateDirectory("version");
        var prompts = 0;

        var result = await new LocalModManager(version).ImportAsync([first, second], (conflict, _) =>
        {
            prompts++;
            Assert.Equal(second, conflict.SourcePath);
            return Task.FromResult(new ModImportChoice(ModImportDecision.Skip));
        });

        Assert.Equal(1, prompts);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(version, "mods", "same.jar")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Import_RefusesChangedDestinationAfterConflictConsent(bool changeEnabledState)
    {
        using var scope = new TestFileSystemScope();
        var source = scope.GetPath("example.jar");
        await File.WriteAllTextAsync(source, "replacement");
        var mods = scope.CreateDirectory("version/mods");
        var destination = Path.Combine(mods, "example.jar");
        await File.WriteAllTextAsync(destination, "old");
        var updatedPath = changeEnabledState ? destination + ".disabled" : destination;

        var result = await new LocalModManager(scope.GetPath("version")).ImportAsync([source], (_, _) =>
        {
            if (changeEnabledState)
                File.Move(destination, updatedPath);
            else
            {
                File.WriteAllText(destination, "externally changed");
            }
            return Task.FromResult(new ModImportChoice(ModImportDecision.Replace));
        });

        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Equal(changeEnabledState ? "old" : "externally changed", await File.ReadAllTextAsync(updatedPath));
        Assert.Single(Directory.GetFiles(mods));
    }

    [Fact]
    public async Task Import_RevalidatesSourceAfterCallback()
    {
        using var scope = new TestFileSystemScope();
        var source = scope.GetPath("example.jar");
        await File.WriteAllTextAsync(source, "source");
        var mods = scope.CreateDirectory("version/mods");
        var destination = Path.Combine(mods, "example.jar");
        await File.WriteAllTextAsync(destination, "keep");

        var result = await new LocalModManager(scope.GetPath("version")).ImportAsync([source], (_, _) =>
        {
            File.Delete(source);
            Directory.CreateDirectory(source);
            return Task.FromResult(new ModImportChoice(ModImportDecision.Replace));
        });

        Assert.Single(result.Errors);
        Assert.Equal("keep", await File.ReadAllTextAsync(destination));
        Assert.Single(Directory.GetFiles(mods));
    }

    [Fact]
    public async Task Import_VersionDeletedInCallbackIsNotRecreated()
    {
        using var scope = new TestFileSystemScope();
        var version = scope.CreateDirectory("version");
        var mods = scope.CreateDirectory("version/mods");
        var source = scope.GetPath("example.jar");
        await File.WriteAllTextAsync(source, "source");
        await File.WriteAllTextAsync(Path.Combine(mods, "example.jar"), "old");

        var result = await new LocalModManager(version).ImportAsync([source], (_, _) =>
        {
            Directory.Delete(version, true);
            return Task.FromResult(new ModImportChoice(ModImportDecision.Replace));
        });

        Assert.Single(result.Errors);
        Assert.False(Directory.Exists(version));
    }

    [Fact]
    public async Task Import_RejectsLinkedModsDirectory()
    {
        using var scope = new TestFileSystemScope();
        var version = scope.CreateDirectory("version");
        var outside = scope.CreateDirectory("outside");
        var source = scope.GetPath("example.jar");
        await File.WriteAllTextAsync(source, "source");
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(version, "mods"), outside);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }

        await Assert.ThrowsAsync<IOException>(() => new LocalModManager(version).ImportAsync([source]));

        Assert.Empty(Directory.GetFiles(outside));
    }

    [Theory]
    [InlineData("token")]
    [InlineData("choice")]
    [InlineData("exception")]
    public async Task Import_PartialSuccessRaisesChangedEvenWhenBatchStops(string stop)
    {
        using var scope = new TestFileSystemScope();
        using var cancellation = new CancellationTokenSource();
        var version = scope.CreateDirectory("version");
        var mods = scope.CreateDirectory("version/mods");
        var first = scope.GetPath("first.jar");
        var second = scope.GetPath("second.jar");
        await File.WriteAllTextAsync(first, "first");
        await File.WriteAllTextAsync(second, "second");
        await File.WriteAllTextAsync(Path.Combine(mods, "second.jar"), "keep");
        var changed = 0;
        void OnChanged(string directory)
        {
            if (directory == version)
            {
                changed++;
                throw new InvalidOperationException("Subscriber failure must be isolated.");
            }
        }
        LocalModManager.Changed += OnChanged;
        try
        {
            var import = new LocalModManager(version).ImportAsync([first, second], (_, _) =>
            {
                if (stop == "exception")
                    throw new InvalidOperationException("Callback failed after the first import.");
                if (stop == "token")
                {
                    cancellation.Cancel();
                    return Task.FromResult(new ModImportChoice(ModImportDecision.Skip));
                }
                return Task.FromResult(new ModImportChoice(ModImportDecision.Cancel));
            }, cancellation.Token);

            if (stop == "token")
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => import);
            else if (stop == "exception")
                await Assert.ThrowsAsync<InvalidOperationException>(() => import);
            else
                Assert.True((await import).Cancelled);

            Assert.Equal(1, changed);
            Assert.Equal("first", await File.ReadAllTextAsync(Path.Combine(mods, "first.jar")));
            Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(mods, "second.jar")));
            Assert.Equal(2, Directory.GetFiles(mods).Length);
        }
        finally
        {
            LocalModManager.Changed -= OnChanged;
        }
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
