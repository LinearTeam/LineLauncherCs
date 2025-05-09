using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LMC.Minecraft.Download.Model
{
    public class ForgeInstallProfile
    {
        [JsonPropertyName("data")]
        public Dictionary<string, IPData> Data { get; set; }
        [JsonPropertyName("processors")]
        public Processor[] Processors { get; set; }
        [JsonPropertyName("libraries")]
        public VanillaLibraryJson[] Libraries { get; set; }
    }
}