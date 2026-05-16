using LMCCore.Utils;

namespace LMCCore.Game.Versioning;

public sealed class VersionConfigFileCache
{
    private readonly Dictionary<string, JsonUtils?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public JsonUtils? GetOrAdd(string path)
    {
        var normalizedPath = VersionPathUtils.NormalizePath(path);
        if (_cache.TryGetValue(normalizedPath, out var cached))
        {
            return cached?.Clone();
        }

        JsonUtils? loaded = null;
        if (File.Exists(normalizedPath))
        {
            var json = JsonUtils.Parse(File.ReadAllText(normalizedPath));
            loaded = json.IsValid ? json : null;
        }

        _cache[normalizedPath] = loaded?.Clone();
        return loaded?.Clone();
    }
}
