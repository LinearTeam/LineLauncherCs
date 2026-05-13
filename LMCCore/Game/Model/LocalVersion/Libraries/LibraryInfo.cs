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
using LMCCore.Game.Model.LocalVersion.Compatibility;
using System.Text.Json;

namespace LMCCore.Game.Model.LocalVersion.Libraries;


public interface ILibraryInfo
{
    string Name { get; }
}

public class LibraryInfo : ILibraryInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonIgnore]
    public bool? IsNative { get; set; }

    [JsonPropertyName("natives")]
    public Dictionary<string, string>? Natives { get; set; }

    [JsonPropertyName("downloads")]
    public LibraryDownloadInfo? Downloads { get; set; }
    
    [JsonPropertyName("rules")]
    public List<CompatibilityRule>? Rules { get; set; }
}

public class SimpleLibraryInfo : ILibraryInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("sha1")]
    public string? Sha1 { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }

    [JsonPropertyName("md5")]
    public string? Md5 { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("sha512")]
    public string? Sha512 { get; set; }
}

public class LibraryDownloadInfo
{
    [JsonPropertyName("artifact")]
    public DownloadableFileInfo? Artifact { get; set; }

    [JsonPropertyName("classifiers")]
    public Dictionary<string, DownloadableFileInfo>? Classifiers { get; set; }
}

public class LibraryInfoConverter : JsonConverter<ILibraryInfo>
{
    public override ILibraryInfo? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var jsonObject = document.RootElement;

        if (!jsonObject.TryGetProperty("name", out _))
        {
            throw new JsonException("Library entry is missing required field 'name'.");
        }

        if (jsonObject.TryGetProperty("downloads", out _) ||
            jsonObject.TryGetProperty("natives", out _) ||
            jsonObject.TryGetProperty("rules", out _) ||
            jsonObject.TryGetProperty("path", out _))
        {
            return JsonSerializer.Deserialize<LibraryInfo>(jsonObject.GetRawText(), options);
        }

        return JsonSerializer.Deserialize<SimpleLibraryInfo>(jsonObject.GetRawText(), options);
    }

    public override void Write(Utf8JsonWriter writer, ILibraryInfo value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object)value, options);
    }
}
