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
namespace LMCCore.Java.Discovery;

internal static class JavaInstallationInfoParser
{
    public static LocalJava Parse(string path, IEnumerable<string> releaseLines, bool isWindows)
    {
        var lines = releaseLines.ToArray();
        var javaVersion = GetQuotedValue(lines, "JAVA_VERSION")
            ?? throw new InvalidOperationException("JAVA_VERSION is missing");

        var java = new LocalJava
        {
            Path = JavaPathNormalizer.NormalizeRootPath(path),
            Version = ParseJavaVersion(javaVersion),
            Implementor = GetQuotedValue(lines, "IMPLEMENTOR"),
            IsJdk = File.Exists(Path.Combine(path, "bin", isWindows ? "javac.exe" : "javac")),
            Is64Bit = Is64BitArchitecture(GetQuotedValue(lines, "OS_ARCH"))
        };

        return java;
    }

    private static Version ParseJavaVersion(string value)
    {
        var parsed = Version.Parse(value.Replace("_", ".", StringComparison.Ordinal));
        if (parsed.Major != 1 || parsed.Minor <= 0)
        {
            return parsed;
        }

        return new Version(
            parsed.Minor,
            Math.Max(parsed.Build, 0),
            Math.Max(parsed.Revision, 0));
    }

    private static bool Is64BitArchitecture(string? architecture)
    {
        if (string.IsNullOrWhiteSpace(architecture))
        {
            return Environment.Is64BitOperatingSystem;
        }

        return architecture.Contains("64", StringComparison.OrdinalIgnoreCase) ||
               architecture.Equals("aarch64", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetQuotedValue(IEnumerable<string> lines, string key)
    {
        foreach (var line in lines)
        {
            var normalizedLine = line.Replace("=", ":", StringComparison.Ordinal);
            if (!normalizedLine.StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return normalizedLine[(key.Length + 1)..].Trim().Trim('"');
        }

        return null;
    }
}
