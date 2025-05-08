namespace LMC.Minecraft.Profile.Model
{
    public enum ModLoaderType
    {
        Vanilla = 0,
        Forge = 1,
        NeoForge = 2,
        Fabric = 3,
        Other = 4
    }

    public class ModLoader
    {
        public ModLoaderType ModLoaderType { get; set; }
        public string LoaderVersion { get; set; }
    }
}