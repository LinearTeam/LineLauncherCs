namespace LMC.Minecraft
{
    public class LocalProfile
    {
        public string Version { get; set; }
        public string Name { get; set; }
        public string IconPath { get; set; }
        public ModLoader ModLoader { get; set; }
        public string Path { get; set; }
        public ProfileStatus Status { get; set; }
        public GamePath GamePath { get; set; }
    }
}