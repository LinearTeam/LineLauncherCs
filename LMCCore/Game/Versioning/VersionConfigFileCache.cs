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

            if (stamp == null)
            {
                _cache[normalizedPath] = new CacheEntry(null, null);
                return null;
            }

            if (!TryLoadJson(normalizedPath, out var loaded))
            {
                return null;
            }

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

    private static bool TryLoadJson(string normalizedPath, out JsonUtils? json)
    {
        try
        {
            var loaded = JsonUtils.Parse(File.ReadAllText(normalizedPath));
            json = loaded.IsValid ? loaded : null;
            return true;
        }
        catch (IOException)
        {
            json = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            json = null;
            return false;
        }
    }

    private sealed record CacheEntry(string? FileStamp, JsonUtils? Value);
}
