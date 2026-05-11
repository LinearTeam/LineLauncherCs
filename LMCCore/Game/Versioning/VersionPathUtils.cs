namespace LMCCore.Game.Versioning;

internal static class VersionPathUtils
{
    public static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static string NormalizePathOrEmpty(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : NormalizePath(path);
    }
}
