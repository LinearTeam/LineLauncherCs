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
using System.Text.Json.Nodes;
using LMC;
using LMCCore.Game.Model;
using LMCCore.Game.Versioning;
using LMCCore.Game.Versioning.Configuration.Support;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning.Configuration;

public class LMCDataDirectoryConfigSource : IVersionConfigSource
{
    private readonly string _configFilePath;

    public LMCDataDirectoryConfigSource(string? configFilePath = null)
    {
        _configFilePath = configFilePath ?? Path.Combine(Current.LMCPath, "version_configs.json");
    }

    public VersionConfigSourceType SourceType => VersionConfigSourceType.LMCDataDirectory;

    public JsonUtils? TryLoad(LocalGameVersionEntry version, VersionConfigFileCache cache)
    {
        var json = cache.GetOrAdd(_configFilePath);
        if (json is not { IsValid: true, Node: JsonObject jsonObject })
        {
            return null;
        }

        var normalizedVersionDirectory = VersionPathUtils.NormalizePath(version.VersionDirectory);

        foreach (var property in jsonObject)
        {
            if (!string.Equals(VersionPathUtils.NormalizePath(property.Key), normalizedVersionDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value is not JsonObject valueObject)
            {
                return null;
            }

            return JsonUtils.Parse(valueObject.ToJsonString(JsonUtils.DefaultSerializerOptions));
        }

        return null;
    }
}
