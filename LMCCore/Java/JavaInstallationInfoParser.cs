namespace LMCCore.Java;

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
