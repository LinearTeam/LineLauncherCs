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

using System.Text.Json.Serialization;
using LMCCore.Game.Model.LocalVersion.Arguments;
using LMCCore.Game.Model.LocalVersion.Libraries;

namespace LMCCore.Game.Model.LocalVersion;

public class LocalVersionInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("mainClass")]
    public required string MainClass { get; set; }

    [JsonPropertyName("libraries")]
    public required List<ILibraryInfo> Libraries { get; set; }

    [JsonIgnore]
    public string? JavaVersion { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("type")]
    public string? Type { get; set; }
    
    [JsonPropertyName("time")]
    public DateTimeOffset? Time { get; set; }
    
    [JsonPropertyName("releaseTime")]
    public DateTimeOffset? ReleaseTime { get; set; }
    
    /// <summary>
    /// 旧版Minecraft参数
    /// </summary>
    [JsonPropertyName("minecraftArguments")]
    public string? MinecraftArguments { get; set; }
    
    [JsonPropertyName("arguments")]
    public Dictionary<string, List<IGameArgument>>? Arguments { get; set; }
    
    [JsonPropertyName("assets")]
    public string? Assets { get; set; }
    
    [JsonPropertyName("assetIndex")]
    public AssetIndexInfo? AssetIndex { get; set; }
    
    [JsonPropertyName("downloads")]
    public Dictionary<string, DownloadableFileInfo>? Downloads { get; set; }
    
    [JsonPropertyName("clientVersion")]
    public string? ClientVersion { get; set; }

    [JsonPropertyName("patches")]
    public List<HMCLPatchInfo>? Patches { get; set; }

}
public class HMCLPatchInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
public class AssetIndexInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("sha1")]
    public string? Sha1 { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("totalSize")]
    public long TotalSize { get; set; }
}


public class AssetInfo
{
    [JsonPropertyName("hash")]
    public required string Hash { get; set; }
    
    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public class DownloadableFileInfo
{
    [JsonPropertyName("path")]
    public string? Path { get; set; }
    
    [JsonPropertyName("sha1")]
    public string? Sha1 { get; set; }
    
    [JsonPropertyName("size")]
    public long? Size { get; set; }
    
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
