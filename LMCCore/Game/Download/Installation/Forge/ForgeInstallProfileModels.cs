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
using System.Text.Json;
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Download.Installation.Forge;

internal sealed class ForgeInstallProfile
{
    [JsonPropertyName("spec")]
    public int? Spec { get; set; }

    [JsonPropertyName("install")]
    public JsonElement? Install { get; set; }

    [JsonPropertyName("versionInfo")]
    public JsonElement? VersionInfo { get; set; }

    [JsonPropertyName("libraries")]
    public List<ForgeInstallLibrary>? Libraries { get; set; }

    [JsonPropertyName("processors")]
    public List<ForgeProcessorDefinition>? Processors { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, ForgeInstallProfileDataEntry>? Data { get; set; }
}

internal sealed class ForgeInstallLibrary
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("downloads")]
    public ForgeInstallLibraryDownloads? Downloads { get; set; }
}

internal sealed class ForgeInstallLibraryDownloads
{
    [JsonPropertyName("artifact")]
    public DownloadableFileInfo? Artifact { get; set; }
}

internal sealed class ForgeProcessorDefinition
{
    [JsonPropertyName("sides")]
    public List<string>? Sides { get; set; }

    [JsonPropertyName("jar")]
    public required string Jar { get; set; }

    [JsonPropertyName("classpath")]
    public List<string>? Classpath { get; set; }

    [JsonPropertyName("args")]
    public List<string>? Args { get; set; }
}

internal sealed class ForgeInstallProfileDataEntry
{
    [JsonPropertyName("client")]
    public string? Client { get; set; }

    [JsonPropertyName("server")]
    public string? Server { get; set; }
}
