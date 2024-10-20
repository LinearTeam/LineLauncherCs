using LMC.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMC.Basic
{
    public class LMCVersion
    {
        public string Version { get; set; }
        public string Build { get; set; }
        public string Type { get; set; }
        public string GitUrl { get; set; }
        public string HyuUrl { get; set; }
        public bool SecurityOrEmergency { get; set; }
    }
    public class UpdateChecker
    {
        private static Logger s_logger = new Logger("UC");
        public async static Task<LMCVersion> Check()
        {
            s_logger.Info("正在检查更新...");
            var manifest = await HttpUtils.GetString("https://huangyu.win/LMC/versionmanifest.line");
            if (string.IsNullOrEmpty(manifest))
            {
                s_logger.Warn("检查失败，manifest为空");
                return null;
            }
            LineFileParser parser = new LineFileParser();
            Directory.CreateDirectory("./LMC/temp/");
            File.WriteAllText("./LMC/temp/versionManifest.line", manifest);
            string file = "./LMC/temp/versionManifest.line";
            string version = parser.Read(file, "version", "latest");
            string type = parser.Read(file, "type", "latest");
            string soe = parser.Read(file, "security_emergency", "latest");
            string uh = parser.Read(file, "urlhyu", "latest");
            string ug = parser.Read(file, "urlgit", "latest");
            string build = parser.Read(file, "build", "latest");
            LMCVersion res = new LMCVersion();
            res.Version = version;
            res.Type = type;
            res.HyuUrl = uh;
            res.Build = build;
            res.GitUrl = ug;
            res.SecurityOrEmergency = bool.Parse(soe);
            File.Delete(file);
            return res;
        }

        public static async Task Update(LMCVersion version, bool useGit = false)
        {
            string path = "./LMC/" + version.Version + ".exe";
            Downloader downloader = new Downloader(useGit ? version.GitUrl : version.HyuUrl, path);
            await downloader.DownloadFileAsync();
            string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = Path.GetFileName(fullPath);
            File.WriteAllText("./LMC/update.bat", $"TASKKILL /F /IM \"{fileName}\" /T\ndel \"{fullPath}\"\ncopy \"./{version.Version}.exe\" \"../{fileName}\"");
//            Process.Start("./LMC/update.bat");
        }
    }
}
