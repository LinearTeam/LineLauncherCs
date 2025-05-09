#nullable enable
using System.Text.Json.Serialization;

namespace LMC.Minecraft.Download.Model
{
    public class Processor
    {
        [JsonPropertyName("sides")]
        public string[]? Sides { get; set; }
        [JsonPropertyName("jar")]
        public string Jar  { get; set; }
        [JsonPropertyName("classpath")]
        public string[] ClassPath { get; set; }
        [JsonPropertyName("args")]
        public string[] Args { get; set; }
    }
}