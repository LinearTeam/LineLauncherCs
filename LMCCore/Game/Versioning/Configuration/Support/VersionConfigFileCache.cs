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
using LMCCore.Game.Versioning;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning.Configuration.Support;

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
