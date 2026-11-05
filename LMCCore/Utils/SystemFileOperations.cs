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

using System.Diagnostics;
using Microsoft.VisualBasic.FileIO;

namespace LMCCore.Utils;

public static class SystemFileOperations
{
    public static bool SupportsRecycleBin =>
        OperatingSystem.IsWindows()
        || (OperatingSystem.IsMacOS() && File.Exists("/usr/bin/osascript"))
        || (OperatingSystem.IsLinux() && (FindCommand("gio") != null || FindCommand("trash-put") != null));

    public static Task DeleteFileAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            path = Path.GetFullPath(path);
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
                return;

            if (OperatingSystem.IsWindows())
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin,
                    UICancelOption.ThrowException);
            }
            else if (OperatingSystem.IsMacOS() && File.Exists("/usr/bin/osascript"))
            {
                // Foundation uses the native Trash API without automating Finder.
                const string script = """
                    ObjC.import('Foundation');
                    function run(argv) {
                        var error = Ref();
                        if (!$.NSFileManager.defaultManager.trashItemAtURLResultingItemURLError(
                            $.NSURL.fileURLWithPath(argv[0]), null, error)) {
                            throw new Error(ObjC.unwrap(error[0].localizedDescription));
                        }
                    }
                    """;
                await RunCommandAsync("/usr/bin/osascript",
                    ["-l", "JavaScript", "-e", script, "--", path], cancellationToken).ConfigureAwait(false);
            }
            else if (OperatingSystem.IsLinux() && FindCommand("gio") is { } gio)
            {
                await RunCommandAsync(gio, ["trash", path], cancellationToken).ConfigureAwait(false);
            }
            else if (OperatingSystem.IsLinux() && FindCommand("trash-put") is { } trash)
            {
                await RunCommandAsync(trash, ["--", path], cancellationToken).ConfigureAwait(false);
            }
            else
            {
                File.Delete(path);
            }
        }, cancellationToken);

    public static Task RevealFileAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            path = Path.GetFullPath(path);
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
                throw new FileNotFoundException("File not found.", path);

            if (OperatingSystem.IsWindows())
            {
                var startInfo = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                startInfo.ArgumentList.Add("/select,");
                startInfo.ArgumentList.Add(path);
                Process.Start(startInfo)?.Dispose();
            }
            else if (OperatingSystem.IsMacOS())
            {
                await RunCommandAsync("/usr/bin/open", ["-R", path], cancellationToken).ConfigureAwait(false);
            }
            else if (OperatingSystem.IsLinux())
            {
                var dbus = FindCommand("dbus-send");
                if (dbus != null)
                {
                    try
                    {
                        await RunCommandAsync(dbus, ["--session", "--print-reply",
                            "--dest=org.freedesktop.FileManager1", "/org/freedesktop/FileManager1",
                            "org.freedesktop.FileManager1.ShowItems",
                            "array:string:" + new Uri(path).AbsoluteUri, "string:"], cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }
                    catch (IOException)
                    {
                        // Some file managers only support opening the containing directory.
                    }
                }

                await RunCommandAsync(FindCommand("xdg-open") ?? "xdg-open",
                    [Path.GetDirectoryName(path)!], cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new PlatformNotSupportedException("Revealing files is not supported on this platform.");
            }
        }, cancellationToken);

    private static string? FindCommand(string name)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static async Task RunCommandAsync(
        string command, string[] arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo) ?? throw new IOException($"Could not start '{command}'.");
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await output.ConfigureAwait(false);
            var errorText = await error.ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new IOException($"'{command}' failed ({process.ExitCode}): {errorText.Trim()}");
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
