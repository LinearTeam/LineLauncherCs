using LMCCore.Utils;

namespace LMCCore.Game.Versioning;

public sealed class VersionConfigFileCache
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public JsonUtils? GetOrAdd(string path)
    {
        var normalizedPath = VersionPathUtils.NormalizePath(path);
        lock (_syncRoot)
        {
            var stamp = GetFileStamp(normalizedPath);
            if (_cache.TryGetValue(normalizedPath, out var cached) && cached.FileStamp == stamp)
            {
                return cached.Value?.Clone();
            }

            var loaded = LoadJsonUnsafe(normalizedPath, stamp);
            _cache[normalizedPath] = new CacheEntry(stamp, loaded?.Clone());
            return loaded?.Clone();
        }
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

    private static JsonUtils? LoadJsonUnsafe(string normalizedPath, string? stamp)
    {
        if (stamp == null)
        {
            return null;
        }

        try
        {
            var json = JsonUtils.Parse(File.ReadAllText(normalizedPath));
            return json.IsValid ? json : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed record CacheEntry(string? FileStamp, JsonUtils? Value);
}
