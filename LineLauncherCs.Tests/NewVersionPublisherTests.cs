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

using LMCCore.Game.Versioning;

namespace LineLauncherCs.Tests;

public sealed class NewVersionPublisherTests
{
    [Fact]
    public async Task Publish_ExposesOnlyCompletedDirectoryAndDoesNotReplaceExistingVersion()
    {
        using var scope = new TestFileSystemScope();
        var root = scope.CreateDirectory("game");
        var target = Path.Combine(root, "versions", "Example");
        await NewVersionPublisher.PublishAsync(root, "Example", async (stagingRoot, token) =>
        {
            Assert.False(Directory.Exists(target));
            var staging = Directory.CreateDirectory(Path.Combine(stagingRoot, "versions", "Example")).FullName;
            await File.WriteAllTextAsync(Path.Combine(staging, "Example.json"), "{}", token);
        }, () => Assert.True(File.Exists(Path.Combine(target, "Example.json"))), CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() => NewVersionPublisher.PublishAsync(root, "Example",
            (_, _) => throw new InvalidOperationException("Must not build"), () => { }, CancellationToken.None));
        Assert.Equal("{}", await File.ReadAllTextAsync(Path.Combine(target, "Example.json")));
        Assert.Empty(Directory.EnumerateDirectories(root, ".lmc-install-*"));
    }

    [Fact]
    public async Task Publish_FailureRemovesOnlyItsStagingFiles()
    {
        using var scope = new TestFileSystemScope();
        var root = scope.CreateDirectory("game");
        var existing = scope.CreateDirectory("game/versions/Existing");
        await File.WriteAllTextAsync(Path.Combine(existing, "keep.txt"), "keep");

        await Assert.ThrowsAsync<IOException>(() => NewVersionPublisher.PublishAsync(root, "New",
            async (stagingRoot, token) =>
            {
                var temporary = Directory.CreateDirectory(Path.Combine(stagingRoot, "versions", "New")).FullName;
                await File.WriteAllTextAsync(Path.Combine(temporary, "partial.txt"), "partial", token);
                throw new IOException("Simulated failure");
            }, () => { }, CancellationToken.None));

        Assert.True(File.Exists(Path.Combine(existing, "keep.txt")));
        Assert.False(Directory.Exists(Path.Combine(root, "versions", "New")));
        Assert.Empty(Directory.EnumerateDirectories(root, ".lmc-install-*"));
    }

    [Fact]
    public async Task Publish_CancellationBeforeCommitLeavesNoFinalDirectory()
    {
        using var scope = new TestFileSystemScope();
        var root = scope.CreateDirectory("game");
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NewVersionPublisher.PublishAsync(root, "New",
            (stagingRoot, _) =>
            {
                Directory.CreateDirectory(Path.Combine(stagingRoot, "versions", "New"));
                cancellation.Cancel();
                return Task.CompletedTask;
            }, () => { }, cancellation.Token));

        Assert.False(Directory.Exists(Path.Combine(root, "versions", "New")));
        Assert.Empty(Directory.EnumerateDirectories(root, ".lmc-install-*"));
    }
}
