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
            Version = Version.Parse(javaVersion.Replace("_", ".", StringComparison.Ordinal)),
            Implementor = GetQuotedValue(lines, "IMPLEMENTOR"),
            IsJdk = File.Exists(Path.Combine(path, "bin", isWindows ? "javac.exe" : "javac"))
        };

        return java;
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
