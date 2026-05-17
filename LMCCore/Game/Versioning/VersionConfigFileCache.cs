using LMCCore.Utils;

namespace LMCCore.Game.Versioning;

public sealed class VersionConfigFileCache
{
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public JsonUtils? GetOrAdd(string path)
    {
        var normalizedPath = VersionPathUtils.NormalizePath(path);
        var stamp = GetFileStamp(normalizedPath);
        if (_cache.TryGetValue(normalizedPath, out var cached) && cached.FileStamp == stamp)
        {
            return cached.Value?.Clone();
        }

        JsonUtils? loaded = null;
        if (stamp != null)
        {
            var json = JsonUtils.Parse(File.ReadAllText(normalizedPath));
            loaded = json.IsValid ? json : null;
        }

        _cache[normalizedPath] = new CacheEntry(stamp, loaded?.Clone());
        return loaded?.Clone();
    }

    private static string? GetFileStamp(string normalizedPath)
    {
        if (!File.Exists(normalizedPath))
        {
            return null;
        }

        var info = new FileInfo(normalizedPath);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    private sealed record CacheEntry(string? FileStamp, JsonUtils? Value);
}
