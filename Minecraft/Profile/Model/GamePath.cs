using LMC.Pages;
using LMC.Pages.ProfilePage;

namespace LMC.Minecraft.Profile.Model
{
    public class GamePath
    {
        public string Path { get; set; }
        public string Name { get; set; }

        public GamePath(GamePathItem gpi)
        {
            Path = gpi.Path;
            Name = gpi.Name;
        }

        public GamePath()
        { }

        public GamePath(string name, string path)
        {
            Path = path;
            Name = name;
        }
    }
}