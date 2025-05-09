using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using LMC.Account;
using LMC.Basic;
using LMC.Minecraft.Download;
using LMC.Minecraft.Download.Model;
using LMC.Minecraft.Launch.Model;
using LMC.Minecraft.Profile;
using LMC.Minecraft.Profile.Model;
using LMC.Tasks;
using LMC.Utils;

namespace LMC.Minecraft.Launch
{
    public class GameLauncher
    {
        private static Logger s_logger = new Logger("GL");
        public string GamePath { get; set; }
        public int SubTaskForLaunchGame(string game, int taskId, int root)
        {
            Account.Model.Account account = null;
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                TaskManager.Values.Add(taskId, new Dictionary<string, object>());
                account = await AccountManager.GetSelectedAccount();
                account.AccessToken = "0000000000";
                account.Uuid = Guid.NewGuid().ToString();
                var jsonPath = Path.Combine(GamePath, "versions", game, game + ".json");
                var mcJson = JsonSerializer.Deserialize<MinecraftJson>(File.ReadAllText(jsonPath));
                var jsonStr = File.ReadAllText(jsonPath);
                var libs = DownloadTools.ParseLibraryFiles(JsonUtils.GetValueFromJson(jsonStr, "libraries"));
                var major = JsonUtils.GetValueFromJson(jsonStr, "javaVersion.majorVersion");
                TaskManager.Values[taskId].Add("mcJson", mcJson);
                TaskManager.Values[taskId].Add("major", major);
                TaskManager.Values[taskId].Add("json", jsonStr);
                TaskManager.Values[taskId].Add("libs", libs);
            }, "解析版本信息");
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                var start = DateTime.Now;
                GameDownloader gd = new GameDownloader();
                var libs = TaskManager.Values[taskId]["libs"] as List<LibraryFile>;
                string backup;
                var totalLibs = DownloadTools.LibraryFilesToNetFiles(libs, GamePath);
                string indexJson = File.ReadAllText($"{GamePath}/versions/{game}/{game}.json");
                await gd.DownloadFilesAsync(totalLibs);

                var end = DateTime.Now;
                s_logger.Info($"依赖库文件下载耗时{end.Subtract(start).TotalSeconds}s");
            }, "补全游戏依赖库");
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                GameDownloader gd = new GameDownloader();
                var indexJson = TaskManager.Values[taskId]["mcJson"] as MinecraftJson;
                string url = indexJson.AssetIndex.Url;
                string sha1 = indexJson.AssetIndex.Sha1;
                string path = $"{GamePath}/assets/indexes/{indexJson.AssetIndex.Id}.json";
                if ((await gd.CalculateFileSHA1(path)).ToLower() != sha1.ToLower())
                {
                    var indexFile = new NetFile(){
                        Url = url,
                        Hash = sha1,
                        Path = path
                    };

                    await gd.DownloadFileAsync(indexFile);                    
                }

                var source = new DownloadSource();
                source.Bmclapi();
                var assets = DownloadTools.ParseAssetObjects(File.ReadAllText(path), source, GamePath);
                var start = DateTime.Now;
                string backup;
                await gd.DownloadFilesAsync(assets).ConfigureAwait(false);
                
                var end = DateTime.Now;

                s_logger.Info($"资源文件下载耗时{end.Subtract(start).TotalSeconds}s");
                CopyFolder(Path.Combine(GamePath, "assets", "objects"), Path.Combine(GamePath, "assets", "virtual", "legacy"));
                
            }, "补全游戏资源文件");
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                Directory.CreateDirectory(Path.Combine(GamePath, "versions", game, game + "-natives"));
                var libs = TaskManager.Values[taskId]["libs"] as List<LibraryFile>;
                foreach (var lib in libs)
                {
                    if(!lib.IsNativeLib) continue;
                    using var fs = new FileStream(Path.Combine(GamePath, "libraries", lib.Path), FileMode.Open);
                    using ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read);
                    archive.Entries.ForEach(e =>
                    {
                        if (e.Name.EndsWith(".dll"))
                        {
                            e.ExtractToFile(Path.Combine(GamePath, "versions", game, $"{game}-natives", e.Name), true);
                        }
                    });
                }
            },"解压库文件");
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                var mcJson = TaskManager.Values[taskId]["mcJson"] as MinecraftJson;
                var libs = TaskManager.Values[taskId]["libs"] as List<LibraryFile>;
                var json = TaskManager.Values[taskId]["json"] as string;

                #region JvmArgs

                StringBuilder argBuilder = new StringBuilder();
                argBuilder.Append("-Xmx4G -XX:+UseG1GC -Dos.name=\"Windows 10\" -Dos.version=10.0 ");
                Dictionary<string, string> gameArguments = new Dictionary<string, string>();
                gameArguments.Add("${classpath_separator}", ";");
                gameArguments.Add("${natives_directory}", $"\"{Path.Combine(GamePath, "versions", game, game + "-natives")}\"");
                gameArguments.Add("${library_directory}", $"\"{Path.Combine(GamePath, "libraries")}\"");
                gameArguments.Add("${libraries_directory}", $"\"{Path.Combine(GamePath, "libraries")}\"");
                gameArguments.Add("${launcher_name}", "LMC");
                gameArguments.Add("${launcher_version}", App.LauncherVersion);
                gameArguments.Add("${version_name}", game);
                gameArguments.Add("${version_type}", mcJson.Type);
                gameArguments.Add("${game_directory}", $"\"{Path.Combine(GamePath, "versions", game)}\"");
                gameArguments.Add("${assets_root}", $"\"{Path.Combine(GamePath, "assets")}\"");
                gameArguments.Add("${user_properties}", "{}");
                gameArguments.Add("${auth_player_name}", account.Id);
                gameArguments.Add("${auth_uuid}", account.Uuid);
                gameArguments.Add("${auth_access_token}", account.AccessToken);
                gameArguments.Add("${access_token}", account.AccessToken);
                gameArguments.Add("${auth_session}", account.AccessToken);
                gameArguments.Add("${user_type}", account.Type == AccountType.MSA ? "msa" : "legacy");
                gameArguments.Add("${game_assets}", $"\"{Path.Combine(GamePath, @"assets\virtual\legacy")}\"");
                gameArguments.Add("${assets_index_name}", mcJson.AssetIndex.Id);


                if (!string.IsNullOrEmpty(JsonUtils.GetValueFromJson(json, "arguments.jvm")))
                {
                    var node = JsonNode.Parse(JsonUtils.GetValueFromJson(json, "arguments.jvm")).AsArray();
                    foreach (var arg in node)
                    {
                        var argStr = arg.ToString();
                        if (!string.IsNullOrEmpty(JsonUtils.GetValueFromJson(argStr, "rules")))
                        {
                            var rules = JsonSerializer.Deserialize<Rule[]>(JsonUtils.GetValueFromJson(argStr, "rules"));
                            bool windows = false;
                            foreach (var r in rules)
                            {
                                if (r.Os != null && (r.Os.Arch == "x86" || r.Os.System == "windows"))
                                {
                                    windows = true;
                                }
                            }

                            if (windows)
                            {
                                argBuilder.Append($"{ReplaceArgs(gameArguments, JsonUtils.GetValueFromJson(argStr, "value"))} ");
                                continue;
                            }
                            continue;
                        }
                        argBuilder.Append(ReplaceArgs(gameArguments, arg.ToString()) + " ");
                    }
                }
                
                argBuilder.Append(" -cp \"");
                
                foreach (var lib in libs)
                {
                    argBuilder.Append(Path.Combine(GamePath, "libraries", lib.Path) + ";");
                }
                
                argBuilder.Append(Path.Combine(GamePath, "versions", game, game + ".jar") + "\" ");

                argBuilder.Append(mcJson.MainClass + " ");
                if (!string.IsNullOrEmpty(JsonUtils.GetValueFromJson(json, "arguments.game")))
                {
                    var node = JsonNode.Parse(JsonUtils.GetValueFromJson(json, "arguments.game")).AsArray();
                    foreach (var arg in node)
                    {
                        var argStr = arg.ToString();
                        if (!string.IsNullOrEmpty(JsonUtils.GetValueFromJson(argStr, "rules")))
                        {
                            var rules = JsonSerializer.Deserialize<Rule[]>(JsonUtils.GetValueFromJson(argStr, "rules"));
                            bool windows = false;
                            foreach (var r in rules)
                            {
                                if (r.Os != null && (r.Os.Arch == "x86" || r.Os.System == "windows"))
                                {
                                    windows = true;
                                }
                            }

                            if (windows)
                            {
                                argBuilder.Append($"{ReplaceArgs(gameArguments, JsonUtils.GetValueFromJson(argStr, "value"))} ");
                                continue;
                            }
                            continue;
                        }
                        argBuilder.Append(ReplaceArgs(gameArguments, arg.ToString()) + " ");
                    }
                }

                TaskManager.Values[taskId].Add("launchArgs", argBuilder.ToString());

                #endregion

            }, "生成启动命令");
            return root;
        }

        private string ReplaceArgs(Dictionary<string, string> args, string arg)
        {
            args.ForEach(kvp => arg = arg.Replace(kvp.Key, kvp.Value).Trim());
            return arg;
        }
        
        public int CopyFolder(string sourceFolder, string destFolder)
        {
            try
            {
                if (!System.IO.Directory.Exists(destFolder))
                {
                    System.IO.Directory.CreateDirectory(destFolder);
                }
                string[] files = System.IO.Directory.GetFiles(sourceFolder);
                foreach (string file in files)
                {
                    string name = System.IO.Path.GetFileName(file);
                    string dest = System.IO.Path.Combine(destFolder, name);
                    System.IO.File.Copy(file, dest);
                }
                string[] folders = System.IO.Directory.GetDirectories(sourceFolder);
                foreach (string folder in folders)
                {
                    string name = System.IO.Path.GetFileName(folder);
                    string dest = System.IO.Path.Combine(destFolder, name);
                    CopyFolder(folder, dest);
                }
                return 1;
            }
            catch (Exception e)
            {
                return 0;
            }

        }
    }
}