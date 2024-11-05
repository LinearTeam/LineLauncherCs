using System.Collections.Generic;
using System.IO;
using LMC.Basic;

namespace LMC.Minecraft
{
    public class ProfileManager
    {
        public static List<GamePath> GetGamePaths()
        {
            List<GamePath> gamePaths = new List<GamePath>();
            var keys = Config.ReadKeySetGlobal("GamePath");
            foreach (var key in keys)
            {
                GamePath gamePath = new GamePath();
                gamePath.Name = key;
                gamePath.Path = Config.ReadGlobal("GamePath", key);
                gamePaths.Add(gamePath);
            }
            return gamePaths;
        }

        public static List<LocalProfile> GetProfiles(GamePath gamePath)
        {
            List<LocalProfile> profiles = new List<LocalProfile>();
            var dirs = Directory.GetDirectories(gamePath.Path);
            foreach (var dir in dirs)
            {
                
            }
            return profiles;
        }

        public static string GetProfileVersion(string filePath)
        {
            string version = "";
            if (File.Exists(filePath + "\\LMC\\version.line"))
            {
                LineFileParser lineFileParser = new LineFileParser();
                version = lineFileParser.Read($"{filePath}/LMC/version.line", "name", "base");
                if (!string.IsNullOrEmpty(version)) return version;
            }

            if (File.Exists(filePath + "\\PCL\\Setup.ini"))
            {
                var lines = File.ReadLines($"{filePath}/PCL/Setup.ini");
                foreach (var line in lines)
                {
                    if (line.StartsWith("VersionOriginal:"))
                    {
                        version = line.Substring(16);
                    }
                }
                if (!string.IsNullOrEmpty(version)) return version;
            }
            
            
            
            return version;
        }
    }
}