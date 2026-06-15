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

namespace LMCCore.Game.Download.Installation.Forge;

internal static class ForgeLibraryPathHelper
{
    public static string? GetLibPath(string lib)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lib);

        var sp = lib.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (sp.Length < 3)
        {
            return null;
        }

        var package = sp[0];
        var name = sp[1];
        var version = sp[2];
        var normalizedSegments = sp.ToArray();
        var extension = "jar";

        for (var i = 2; i < normalizedSegments.Length; i++)
        {
            var atIndex = normalizedSegments[i].LastIndexOf('@');
            if (atIndex < 0 || atIndex >= normalizedSegments[i].Length - 1)
            {
                continue;
            }

            extension = normalizedSegments[i][(atIndex + 1)..];
            normalizedSegments[i] = normalizedSegments[i][..atIndex];
            break;
        }

        version = normalizedSegments[2];
        var result = package.Replace('.', '/') + "/";
        result += name + "/";
        result += version + "/";

        for (var i = 1; i < normalizedSegments.Length; i++)
        {
            result += normalizedSegments[i] + "-";
        }

        result = result[..^1];
        result += $".{extension}";
        return Path.Combine(".minecraft", "libraries", result.Replace('/', Path.DirectorySeparatorChar));
    }

    public static string GetAbsoluteLibraryPath(string rootPath, string lib)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var relativePath = GetLibPath(lib)
                           ?? throw new InvalidOperationException($"Failed to resolve library path for '{lib}'.");
        return Path.Combine(rootPath, relativePath.Replace(".minecraft", string.Empty).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}
