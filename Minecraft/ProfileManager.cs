using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LMC.Basic;
using LMC.Utils;

namespace LMC.Minecraft
{
    public class ProfileManager
    {
        private static Logger s_logger = new Logger("PMA");

        public static GamePath GetSelectedGamePath()
        {
            GamePath gamePath = new GamePath();
            var key = Config.ReadGlobal("Main", "GamePath");
            if (string.IsNullOrEmpty(key))
            {
                var gps = GetGamePaths();
                if (gps.Count != 1)
                {
                    SetSelectedGamePath(gps.First());
                }
                return gps.First();
            }
            gamePath.Name = key;
            gamePath.Path = Config.ReadGlobal("GamePath", key);
            return gamePath;
        }

        public static void SetSelectedGamePath(GamePath gamePath)
        {
            Config.WriteGlobal("Main", "GamePath", gamePath.Name);
        }

        public static void AddGamePath(GamePath gamePath)
        {
            Config.WriteGlobal("GamePath", gamePath.Name, gamePath.Path);
        }
        public static List<GamePath> GetGamePaths()
        {
            List<GamePath> gamePaths = new List<GamePath>();
            var keys = Config.ReadKeySetGlobal("GamePath");
            bool hasCurrent = false;
            foreach (var key in keys)
            {
                GamePath gamePath = new GamePath();
                gamePath.Name = key;
                gamePath.Path = Config.ReadGlobal("GamePath", key);
                if (Path.GetFullPath("./minecraft").Equals(Path.GetFullPath(gamePath.Path)))
                {
                    hasCurrent = true;
                }
                gamePaths.Add(gamePath);
            }

            if (gamePaths.Count == 0 || !hasCurrent)
            {
                GamePath gamePath = new GamePath();
                gamePath.Path = Path.GetFullPath("./.minecraft");
                gamePath.Name = "当前文件夹";
                gamePaths.Insert(0,gamePath);
//                AddGamePath();
            }
            return gamePaths;
        }

        public async static Task<List<LocalProfile>> GetProfiles(GamePath gamePath)
        {
            s_logger.Info("加载档案中 : " + gamePath.Path);
            List<LocalProfile> profiles = new List<LocalProfile>();
            var dirs = Directory.GetDirectories(gamePath.Path + "\\versions\\");
            foreach (var dir in dirs)
            {
                if (!File.Exists(Path.Combine(dir, $"{dir.Split('\\').Last()}.json")))
                {
                    LocalProfile profile = new LocalProfile();
                    profile.Status = ProfileStatus.NoJson;
                    profile.Path = dir;
                    profile.GamePath = gamePath;
                    profile.Name = dir.Split('\\').Last();
                    profiles.Add(profile);
                    continue;
                }

                if (!File.Exists(Path.Combine(dir, $"{dir.Split('\\').Last()}.jar")))
                {
                    LocalProfile profile = new LocalProfile();
                    profile.Path = dir;
                    profile.GamePath = gamePath;
                    profile.Name = dir.Split('\\').Last();
                    profile.Status = ProfileStatus.NoJar;
                    profiles.Add(profile);
                    continue;
                }
                
                LocalProfile p = new LocalProfile();
                p.Path = dir;
                p.GamePath = gamePath;
                p.Name = dir.Split('\\').Last();

                try
                {
                    p.Version = await GetProfileVersion(dir);
                    ModLoader ml = new ModLoader();
                    ml.ModLoaderType = ModLoaderType.Vanilla;
                    ml.LoaderVersion = p.Version;
                    p.ModLoader = ml;
                }
                catch (UnknownVersionException e)
                {
                    p.Status = ProfileStatus.Unknown;
                    profiles.Add(p);
                    s_logger.Warn($"发现一个未知版本的档案: {dir}");
                    ModLoader ml = new ModLoader();
                    ml.ModLoaderType = ModLoaderType.Other;
                    ml.LoaderVersion = "Unknown";
                    p.ModLoader = ml;
                    continue;
                }
                catch (Exception e)
                {
                    p.Status = ProfileStatus.Unknown;
                    ModLoader ml = new ModLoader();
                    ml.ModLoaderType = ModLoaderType.Other;
                    ml.LoaderVersion = "Unknown";
                    p.ModLoader = ml;
                    profiles.Add(p);
                    s_logger.Warn($"在解析档案版本 {dir} 时遇到未知错误： {e.Message}\n{e.StackTrace}");
                }
                profiles.Add(p);
            }
            return profiles;
        }

        public async static Task<string> GetProfileVersion(string filePath, bool write = false)
        {
            string version = "";
            if (File.Exists(filePath + "\\LMC\\version.line"))
            {
                LineFileParser lineFileParser = new LineFileParser();
                version = lineFileParser.Read($"{filePath}/LMC/version.line", "id", "base");
                if (!string.IsNullOrEmpty(version)) return version;
            }
            if(write) Directory.CreateDirectory(filePath + "\\LMC");
            
            if (File.Exists(filePath + "\\PCL\\Setup.ini"))
            {
                var releaseTime = "";
                var lines = File.ReadLines($"{filePath}/PCL/Setup.ini");
                foreach (var line in lines)
                {
                    if (line.StartsWith("VersionOriginal:"))
                    {
                        version = line.Substring(16);
                    }

                    if (line.StartsWith("ReleaseTime:"))
                    {
                        releaseTime = line.Substring(12);
                    }
                }

                if (!string.IsNullOrEmpty(version) && version != "Old")
                {
                    
                    if (write)
                    {
                        LineFileParser lineFileParser = new LineFileParser();
                        File.Create($"{filePath}/LMC/version.line").Close();
                        lineFileParser.Write($"{filePath}/LMC/version.line", "id", version,"base");
                    }
                    return version;
                }
                if (!string.IsNullOrEmpty(releaseTime))
                {
                    GameDownloader gd = new GameDownloader();
                    gd.Bmclapi();
                    var manifest = await gd.GetVersionManifest();
                    var parsed = gd.ParseManifest(manifest);
                    foreach (var ver in parsed.normal)
                    {
                        DateTime date = DateTime.Parse(ver.ReleaseTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
                        DateTime pclTime = DateTime.ParseExact(releaseTime, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                        if (date.CompareTo(pclTime) == 0)
                        {
                            version = ver.Id;
                        }
                    }
                    foreach (var ver in parsed.alpha)
                    {
                        DateTime date = DateTime.Parse(ver.ReleaseTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
                        DateTime pclTime = DateTime.ParseExact(releaseTime, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                        if (date.CompareTo(pclTime) == 0)
                        {
                            version = ver.Id;
                        }
                    }
                    foreach (var ver in parsed.beta)
                    {
                        DateTime date = DateTime.Parse(ver.ReleaseTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
                        DateTime pclTime = DateTime.ParseExact(releaseTime, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
                        if (date.CompareTo(pclTime) == 0)
                        {
                            version = ver.Id;
                        }
                    }
                    //上述为屎山
                }

                if (!string.IsNullOrEmpty(version) && version != "Old")
                {
                    if (write)
                    {
                        LineFileParser lineFileParser = new LineFileParser();
                        File.Create($"{filePath}/LMC/version.line").Close();
                        lineFileParser.Write($"{filePath}/LMC/version.line", "id", version, "base");
                    }

                    return version;
                }
            }


            if (!string.IsNullOrEmpty(JsonUtils.GetValueFromJson(File.ReadAllText($"{filePath}/{filePath.Split('\\').Last()}.json"), "clientVersion")))
            {
                return JsonUtils.GetValueFromJson(File.ReadAllText($"{filePath}/{filePath.Split('\\').Last()}.json"), "clientVersion");
            }
            
            throw new UnknownVersionException("Failed to parse profile version", filePath);
        }
    }
}