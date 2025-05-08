#nullable enable
namespace LMC.Minecraft.Download.Model
{
    public class LibraryFile
    {
        public string? Url { get; set; }
        public string? Sha1 { get; set; }
        public long? Size { get; set; }
        public Rule[] Rules { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsNativeLib { get; set; }
    }
}