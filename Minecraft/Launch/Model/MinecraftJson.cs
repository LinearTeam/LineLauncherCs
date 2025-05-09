using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LMC.Minecraft.Launch.Model
{
    public class MinecraftJson
    {
        [JsonPropertyName("assetIndex")]
        public AssetIndex? AssetIndex { get; set; }
        
        [JsonPropertyName("assets")]
        public string? Assets { get; set; }
        
        [JsonPropertyName("downloads")]
        public Dictionary<string, Download> Downloads { get; set; }
        
        [JsonPropertyName("id")]
        public string Id { get; set; }
        
        [JsonPropertyName("mainClass")]
        public string MainClass { get; set; }
        
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class AssetIndex
    {
        [JsonPropertyName("Id")]
        public string Id { get; set; }
        [JsonPropertyName("sha1")]
        public string Sha1 { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class Download
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("sha1")]
        public string Sha1 { get; set; }
    }

}