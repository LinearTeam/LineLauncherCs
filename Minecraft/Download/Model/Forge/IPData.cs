using System.Text.Json.Serialization;

namespace LMC.Minecraft.Download.Model
{
    public class IPData
    {
        [JsonPropertyName("client")]
        public string Client { get; set; }
        [JsonPropertyName("server")]
        public string Server { get; set; }
    }
}