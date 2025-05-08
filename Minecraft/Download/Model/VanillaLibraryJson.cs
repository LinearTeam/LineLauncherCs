#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LMC.Minecraft.Download.Model
{
    public class VanillaLibraryJson
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("downloads")]
        public VanillaLibraryJsonDownload Downloads { get; set; }
        
        [JsonPropertyName("natives")]
        public Dictionary<string, string>? Natives { get; set; }

        [JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; } = new List<Rule>();
    }

    public class VanillaLibraryJsonDownload
    {
        [JsonPropertyName("artifact")]
        public DownloadFile? Artifact { get; set; }

        [JsonPropertyName("classifiers")]
        public Dictionary<string, DownloadFile>? Classifiers { get; set; }
    }

    public class DownloadFile
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }
        [JsonPropertyName("sha1")]
        public string Sha1 { get; set; }
        [JsonPropertyName("size")]
        public int Size { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}