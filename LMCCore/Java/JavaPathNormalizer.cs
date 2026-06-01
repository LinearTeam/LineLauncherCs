namespace LMCCore.Java;

internal static class JavaPathNormalizer
{
    public static string NormalizeRootPath(string path)
    {
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static IReadOnlyList<string> DistinctNormalizedRoots(IEnumerable<string> paths)
    {
        return paths
            .Select(NormalizeRootPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
