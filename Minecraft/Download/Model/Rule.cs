#nullable enable
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LMC.Minecraft.Download.Model
{
    public class Rule
    {
        [JsonPropertyName("os")]
        public Os? Os { get; set; }
        [JsonPropertyName("features")]
        public Dictionary<string, string>? Features { get; set; }
        [JsonPropertyName("action")]
        public string Action { get; set; }
    }

    public class Os
    {
        [JsonPropertyName("arch")]
        public string? Arch { get; set; }
        [JsonPropertyName("os")]
        public string? System { get; set; }
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }
    
}