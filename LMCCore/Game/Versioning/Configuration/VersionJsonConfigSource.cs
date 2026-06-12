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
using LMCCore.Game.Model;
using LMCCore.Game.Versioning.Configuration.Support;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning.Configuration;

public class VersionJsonConfigSource : IVersionConfigSource
{
    private const string ConfigPropertyName = "LMCConfig";

    public VersionConfigSourceType SourceType => VersionConfigSourceType.VersionJson;

    public JsonUtils? TryLoad(LocalGameVersionEntry version, VersionConfigFileCache cache)
    {
        if (string.IsNullOrWhiteSpace(version.JsonPath) || !File.Exists(version.JsonPath))
        {
            return null;
        }

        var json = cache.GetOrAdd(version.JsonPath);
        if (json is not { IsValid: true, Node: JsonObject jsonObject })
        {
            return null;
        }

        if (!jsonObject.TryGetPropertyValue(ConfigPropertyName, out var configNode) || configNode is not JsonObject)
        {
            return null;
        }

        return JsonUtils.Parse(configNode.ToJsonString(JsonUtils.DefaultSerializerOptions));
    }
}
