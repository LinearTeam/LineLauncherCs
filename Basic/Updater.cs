using LMC.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using iNKORE.UI.WPF.Controls;
using iNKORE.UI.WPF.Modern.Controls;

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
    public class Updater
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
            ContentDialog ctd = new ContentDialog();
            ctd.Title = "更新";
            SimpleStackPanel content = new SimpleStackPanel();
            ProgressRing ring = new ProgressRing();
            Label label = new Label();
            label.Content = "正在下载新版本LMC...";
            content.Children.Add(label);
            content.Children.Add(ring);
            ctd.Content = content;
            ctd.ShowAsync();
            string path = "./LMC/" + version.Version + ".exe";
            Downloader downloader = new Downloader(useGit ? version.GitUrl : version.HyuUrl, path);
            try
            {
                await downloader.DownloadFileAsync();
            }
            catch (Exception ex)
            {
                label.Content = $"更新失败: {ex.Message}\n:{ex.StackTrace}";
                ring.IsIndeterminate = false;
                return;
            }

            string fullPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fileName = Path.GetFileName(fullPath);
            File.WriteAllText("./LMC/update.bat", @$"
@echo off
echo Updating...
title Updating
TASKKILL /F /IM {"\"" + fileName + "\""} /T
timeout /t 2 /nobreak
del {"\"" + fullPath + "\""}
copy {"\"" + Path.GetFullPath("./LMC/" + version.Version + ".exe")+ "\""} {"\"" + fullPath + "\""}
del {"\"" + Path.GetFullPath("./LMC/" + version.Version + ".exe") + "\""}
cd {"\"" + Path.GetFullPath("./")}
{Path.GetFullPath("./").Split(':').First()}:
{"\"" + fullPath + "\""}
exit", new UTF8Encoding(false));
            Process.Start("explorer.exe", $"\"{Path.GetFullPath("./LMC/update.bat")}\"");
            s_logger.Info("正在退出程序以完成更新");
            Environment.Exit(0);
        }
    }
}
 