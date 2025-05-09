using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using LMC.Basic;
using LMC.Basic.Configs;
using LMC.Minecraft.Download.Exceptions;
using LMC.Minecraft.Download.Model;
using LMC.Minecraft.Profile;
using LMC.Tasks;
using LMC.Utils;

namespace LMC.Minecraft.Download
{
    /*
     * GameDownload Tools Class
     */
    public class GameDownloader
    {
        public static string GamePath = @"./.minecraft";
        private readonly Dictionary<string, int> _debug = new Dictionary<string, int>();
        private DownloadSource _downloadSource = new DownloadSource();
        private readonly Logger _logger = new Logger("GD");
        private readonly int _maxRetries = 30;
        private int _remainingFiles;
        private int _totalFiles;

        public GameDownloader(CancellationTokenSource cts)
        {
            _tokenSource = cts;
        }

        public GameDownloader()
        {
        }

        public void ChangeSource(DownloadSource newSource)
        {
            if (newSource == _downloadSource || newSource == null) return;
            _downloadSource = newSource;
        }

        public void Bmclapi()
        {
            _downloadSource.Bmclapi();
        }

        public void LineMirror()
        {
            _downloadSource.LineMirror();
        }

        public async Task<string> GetVersionManifest()
        {
            return await HttpUtils.GetString(_downloadSource.VersionManifest);
        }


        public async Task<string> CalculateFileSHA1(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var fileStream = File.OpenRead(filePath))
                    {
                        using (var sha1 = SHA1.Create())
                        {
                            // 计算哈希值
                            byte[] hashBytes = sha1.ComputeHash(fileStream);

                            // 将哈希字节数组转换为十六进制字符串
                            var sb = new StringBuilder();
                            foreach (byte b in hashBytes) sb.Append(b.ToString("x2"));
                            return sb.ToString();
                        }
                    }
                }
                catch
                {
                    return "";
                }
            });
        }

        public async Task DownloadGame(string versionId, string versionName, bool isOptifine, bool isFabric, bool isForge, string optifine = "", string fabric = "",
            string forge = "", string fabricApi = "")
        {
            GamePath = ProfileManager.GetSelectedGamePath().Path;
            try
            {
                if (!(isOptifine || isFabric || isForge))
                {
                    _logger.Info($"新的原版游戏下载任务: {versionId} , 名称: {versionName}");
                    await DownloadGame(versionId, versionName);
                }

                if (!(isOptifine || isForge) && isFabric)
                {
                    _logger.Info($"新的仅Fabric下载任务: {versionId} : {fabric} : {fabricApi} , 名称: {versionName}");
                    await DownloadGame(versionId, versionName, fabric, fabricApi);
                }

                if (!(isOptifine || isFabric) && isForge)
                {
                    _logger.Info($"新的仅Forge下载任务: {versionId} : {forge} , 名称: {versionName}");
                    await DownloadGame(versionId, versionName, forge);
                }
            }
            catch (Exception e)
            {
                if (e is IBaseException be)
                {
                    _logger.Error(be, "下载游戏");
                }
            }
        }

        private async Task DownloadGame(string versionId, string versionName, string forge)
        {
            var task = TaskManager.Instance.CreateTask(5, "下载 Forge 版本 : " + versionName + $"  ({versionId}-{forge})");
            _tokenSource = task.CancellationTokenSource;
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (s, e) =>
            {
                if (_tokenSource.IsCancellationRequested || task.Status == ExecutionStatus.Failed)
                {
                    try
                    {
                        Directory.Delete($"{GamePath}/versions/{versionName}", true);
                        timer.Stop();
                        timer.IsEnabled = false;
                    }
                    catch
                    {
                    }
                }
            };
            var i = SubTaskForVanilla(versionId, versionName, task.Id, 0);
            i = SubTaskForHighVersionForge(versionId, versionName, forge, task.Id, i);
            TaskManager.Instance.AddSubTask(task.Id, ++i, async ctx =>
            {
                string path = $"{GamePath}/versions/{versionName}";
                Directory.CreateDirectory($"{path}/LMC/");
                File.Create($"{path}/LMC/version.line").Close();
                var lineFileParser = new LineFileParser();
                lineFileParser.Write($"{path}/LMC/version.line", "name", versionName, "base");
                lineFileParser.Write($"{path}/LMC/version.line", "isModPack", "N", "modpack");
                lineFileParser.Write($"{path}/LMC/version.line", "Fabric", "N", "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "Forge", forge, "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "Optifine", "N", "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "id", versionId, "base");
            }, "完成下载");
            
            timer.Start();
            task.Status = ExecutionStatus.Waiting;
            Task.Run(() => TaskManager.Instance.ExecuteTasksAsync());
        }
        
        /// <summary>
        /// 下载Fabric游戏
        /// </summary>
        /// <param name="versionId">Minecraft版本</param>
        /// <param name="versionName">版本名</param>
        /// <param name="fabric">Fabric Loader版本</param>
        /// <param name="fabricApi">Fabric Api版本</param>
        private async Task DownloadGame(string versionId, string versionName, string fabric, string fabricApi = "")
        {
            var task = TaskManager.Instance.CreateTask(5, "下载 Fabric 版本 : " + versionName + $"  ({versionId}-{fabric}) {fabricApi}");
            _tokenSource = task.CancellationTokenSource;
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (s, e) =>
            {
                if (_tokenSource.IsCancellationRequested || task.Status == ExecutionStatus.Failed)
                {
                    try
                    {
                        Directory.Delete($"{GamePath}/versions/{versionName}", true);
                        timer.Stop();
                        timer.IsEnabled = false;
                    }
                    catch
                    {
                    }
                }
            };
            var i = SubTaskForVanilla(versionId, versionName, task.Id, 0);
            i = SubTaskForFabric(versionId, versionName, fabric, task.Id, i, fabricApi);
            TaskManager.Instance.AddSubTask(task.Id, ++i, async ctx =>
            {
                string path = $"{GamePath}/versions/{versionName}";
                Directory.CreateDirectory($"{path}/LMC/");
                File.Create($"{path}/LMC/version.line").Close();
                var lineFileParser = new LineFileParser();
                lineFileParser.Write($"{path}/LMC/version.line", "name", versionName, "base");
                lineFileParser.Write($"{path}/LMC/version.line", "isModPack", "N", "modpack");
                lineFileParser.Write($"{path}/LMC/version.line", "Fabric", fabric, "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "Forge", "N", "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "Optifine", "N", "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "id", versionId, "base");
            }, "完成下载"); 

            timer.Start();
            task.Status = ExecutionStatus.Waiting;
            Task.Run(() => TaskManager.Instance.ExecuteTasksAsync());
        }
        
        /// <summary>
        /// 下载原版游戏
        /// </summary>
        /// <param name="versionId">版本id</param>
        /// <param name="versionName">版本名</param>
        private async Task DownloadGame(string versionId, string versionName)
        {
            var task = TaskManager.Instance.CreateTask(5, $"下载原版游戏 {versionName} ({versionId})");
            _tokenSource = task.CancellationTokenSource;
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += (s, e) =>
            {
                if (_tokenSource.IsCancellationRequested || task.Status == ExecutionStatus.Failed)
                {
                    try
                    {
                        Directory.Delete($"{GamePath}/versions/{versionName}", true);
                        timer.Stop();
                        timer.IsEnabled = false;
                    }
                    catch
                    {
                    }
                }
            };
            var i = SubTaskForVanilla(versionId, versionName, task.Id, 0);
            TaskManager.Instance.AddSubTask(task.Id, ++i, async ctx =>
            {
                string path = $"{GamePath}/versions/{versionName}";
                Directory.CreateDirectory($"{path}/LMC/");
                File.Create($"{path}/LMC/version.line").Close();
                var lineFileParser = new LineFileParser();
                lineFileParser.Write($"{path}/LMC/version.line", "name", versionName, "base");
                lineFileParser.Write($"{path}/LMC/version.line", "isModPack", "N", "modpack");
                lineFileParser.Write($"{path}/LMC/version.line", "Fabric", "N", "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "Forge", "N", "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "Optifine", "N", "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "id", versionId, "base");
            }, "完成下载");
            
            
            timer.Start();
            task.Status = ExecutionStatus.Waiting;
            Task.Run(() => TaskManager.Instance.ExecuteTasksAsync());
        }

        #region DownloadTasks

        private int SubTaskForHighVersionForge(string versionId, string versionName, string forge, int taskId, int root)
        {
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                var installerPath = Path.Combine("LMC", "temp", "forgeInstaller",Guid.NewGuid() + ".jar");
                if (_downloadSource.SourceType == 0)
                {
                    var installerUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{versionId}-{forge}/forge-{versionId}-{forge}-installer.jar";
                    Downloader downloader = new Downloader(installerUrl, installerPath);
                    await downloader.DownloadFileAsync();
                }
                else
                {
                    var installerUrl = $"https://bmclapi2.bangbang93.com/forge/download?mcversion={versionId}&version={forge}&format=jar&category=installer";
                    Downloader downloader = new Downloader(installerUrl, installerPath);
                    await downloader.DownloadFileAsync();
                }
                TaskManager.Values[taskId]["installerPath"] = installerPath;
            }, "获取 Forge 安装器");
            
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                string installerPath = TaskManager.Values[taskId]["installerPath"] as string;
                using Stream fs = new FileStream(installerPath, FileMode.Open);
                ZipArchive installer = new ZipArchive(fs, ZipArchiveMode.Read);
                var ip = installer.GetEntry("install_profile.json");
                using var ipStream = ip.Open();
                ForgeInstallProfile fip = JsonSerializer.Deserialize<ForgeInstallProfile>(ipStream);
                var libraries = new List<LibraryFile>();
                fip.Libraries.ForEach((f) => libraries.AddRange(DownloadTools.VanillaLibraryJsonToLibraryFile(f)));
                using var forgeJsonStream = installer.GetEntry("version.json").Open();
                var forgeJson = JsonNode.Parse(forgeJsonStream);
                libraries.AddRange(DownloadTools.ParseLibraryFiles(forgeJson["libraries"].ToString()));
                var netFiles = DownloadTools.LibraryFilesToNetFiles(libraries, GamePath);
                TaskManager.Values[taskId]["forgeLibs"] = netFiles;
                TaskManager.Values[taskId]["ipModel"] = fip;
                TaskManager.Values[taskId]["forgeJson"] = forgeJson;
            }, "解析依赖库");
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                var netFiles = TaskManager.Values[taskId]["forgeLibs"] as List<NetFile>;
                var backup = "";
                if (_downloadSource.SourceType == 0)
                {
                    var source = new DownloadSource();
                    source.Bmclapi();
                    backup = source.Forge;
                    await DownloadFilesAsync(netFiles, _downloadSource.Forge, backup);
                }
                else
                {
                    var source = new DownloadSource();
                    backup = source.Forge;
                    await DownloadFilesAsync(netFiles, _downloadSource.Forge, backup);
                }
            },"下载 Forge 依赖库");
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                string installerPath = TaskManager.Values[taskId]["installerPath"] as string;
                using Stream fs = new FileStream(installerPath, FileMode.Open);
                ZipArchive installer = new ZipArchive(fs, ZipArchiveMode.Read);
                var clientLzma = installer.GetEntry("client.lzma");
                if (clientLzma != null)
                {
                    using var clientLzmaStream = clientLzma.Open();
                    using var lfs = new FileStream(Path.Combine(GamePath, "libraries", DownloadTools.GetLibraryFile($"net.minecraftforge:forge:{forge}:clientdata@lzma")), FileMode.Create);
                    await clientLzmaStream.CopyToAsync(lfs);
                }
                
                var serverLzma = installer.GetEntry("server.lzma");
                if (serverLzma != null)
                {
                    using var serverLzmaStream = serverLzma.Open();
                    using var lfs = new FileStream(Path.Combine(GamePath,"libraries", DownloadTools.GetLibraryFile($"net.minecraftforge:forge:{forge}:serverdata@lzma")), FileMode.Create);
                    await serverLzmaStream.CopyToAsync(lfs);
                }
                
            }, "解压所需的文件");
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                string installerPath = TaskManager.Values[taskId]["installerPath"] as string;
                using Stream fs = new FileStream(installerPath, FileMode.Open);
                ZipArchive installer = new ZipArchive(fs, ZipArchiveMode.Read);
                var ip = TaskManager.Values[taskId]["ipModel"] as ForgeInstallProfile;
                foreach (var proc in ip.Processors)
                {
                    _logger.Info("处理器：" + proc.Jar);

                    if(proc.Sides != null && !proc.Sides.Contains("client")) continue;
                    var java = JavaManager.GetJavaWithMinVersion(new Version("8.0"));
                    if (java == null)
                    {
                        MainWindow.ShowDialog("确认", "Forge 安装失败，因为启动器内没有 Java 8 以上的 Java 用于执行安装处理器。请在 设置 - 游戏设置 - Java管理 中添加/安装 Java 8 以上的 Java 后重试", "错误").ConfigureAwait(false);
                    }
                    if (java == null) throw new Exception("无可用的 Java 8 以上 Java，请安装/添加 Java 8 以上 Java 后重试。");
                    StringBuilder sb = new StringBuilder($" -cp \"");

                    proc.ClassPath.ForEach((p) => sb.Append(Path.Combine(GamePath, "libraries", DownloadTools.GetLibraryFile(p)) + ";"));
                    sb.Append(Path.Combine(GamePath, "libraries", DownloadTools.GetLibraryFile(proc.Jar)));
                    sb.Append($"\" {DownloadTools.GetMainClassFromJar(Path.Combine(GamePath, "libraries", DownloadTools.GetLibraryFile(proc.Jar)))} ");

                    bool mojmap = false;
                    foreach (var arg in proc.Args)
                    {
                        if (arg.Equals("DOWNLOAD_MOJMAPS"))
                        {
                            mojmap = true;
                            break;
                        }
                        var targ = arg.Replace("{MINECRAFT_JAR}", Path.Combine(GamePath, "versions", versionName, $"{versionName}.jar"));
                        targ = targ.Replace("{BINPATCH}", Path.Combine(GamePath, "libraries", DownloadTools.GetLibraryFile($"net.minecraftforge:forge:{forge}:clientdata@lzma")));
                        targ = targ.Replace("{INSTALLER}", Path.GetFullPath(installerPath));
                        targ = targ.Replace("{SIDE}", "client");
                        targ = targ.Replace("{ROOT}", Path.GetFullPath(GamePath));
                        if (ip.Data.ContainsKey(targ.Replace("{", "").Replace("}", "")))
                        {
                            targ = ip.Data[targ.Replace("{", "").Replace("}", "")].Client;
                        }

                        if (targ.StartsWith("[") && targ.EndsWith("]"))
                        {
                            targ = DownloadTools.GetLibraryFile(targ);
                        }
                        targ = '"' + targ + '"';
                        sb.Append(targ + " ");
                    }

                    if (mojmap)
                    {
                        var vanillaJson = File.ReadAllText(Path.Combine(GamePath, "versions", versionName, versionName + ".json"));
                        var netFile = new NetFile();
                        netFile.Url = JsonUtils.GetValueFromJson(vanillaJson, "downloads.client_mappings.url");
                        netFile.Hash = JsonUtils.GetValueFromJson(vanillaJson, "downloads.client_mappings.sha1");
                        netFile.Path = DownloadTools.GetLibraryFile(ip.Data["MOJMAPS"].Client);
                        _logger.Info("正在下载 MOJMAPS");
                        await DownloadFileAsync(netFile);
                        _logger.Info("已下载 MOJMAPS");
                        continue;
                    }
                    _logger.Info("=====================Processor Info=====================");
                    _logger.Info($"Processor : {proc.Jar}");
                    _logger.Info($"CommandLine: {sb}");
                    _logger.Info($"Java: {java}");
                    _logger.Info("=====================Processor Info=====================");
                    _logger.Info("Runnning...");
                    Process process = new Process();
                    ProcessStartInfo startInfo = new ProcessStartInfo(java.Path);
                    startInfo.Arguments = sb.ToString();
                    startInfo.UseShellExecute = false;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    process.StartInfo = startInfo;
                    process.Start();
                    process.OutputDataReceived += (sender, args) =>
                    {
                        _logger.Info($"[Process][{proc.Jar}] " + args.Data);
                    };
                    process.BeginOutputReadLine(); 
                    DispatcherTimer timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(1);
                    timer.Tick += (sender, args) =>
                    {
                        if (_tokenSource.IsCancellationRequested)
                        {
                            process.Kill();
                            timer.Stop();
                            timer.IsEnabled = false;
                        }
                    };
                    timer.Start();
                    process.WaitForExit();
                    if (_tokenSource.IsCancellationRequested)
                    {
                        _logger.Info("Cancelled process.");
                        return;
                    }
                    int exitCode = process.ExitCode;
                    timer.Stop();
                    timer.IsEnabled = false;
                    _logger.Info($"");
                    _logger.Info("=====================Processor Result=====================");
                    _logger.Info($"Processor : {proc.Jar}");
                    _logger.Info($"CommandLine: {sb}");
                    _logger.Info($"Java: {java}");
                    _logger.Info($"ExitCode: {exitCode}");
                    _logger.Info("=====================Processor Result=====================");
                    if (exitCode != 0)
                    {
                        throw new ProcessorException(exitCode, proc, "执行处理器时退出码不为0。");
                    }
                }
                
                var forgeJson = TaskManager.Values[taskId]["forgeJson"] as JsonNode;
                var vanilla = JsonNode.Parse(File.ReadAllText(Path.Combine(GamePath, "versions", versionName, versionName + ".json")));
                vanilla["mainClass"] = forgeJson["mainClass"].DeepClone();
                var totalLibs = vanilla["libraries"].DeepClone().AsArray();
                foreach (var lib in forgeJson["libraries"].AsArray()) { totalLibs.Add(lib.DeepClone()); }
                vanilla["libraries"] = totalLibs.DeepClone();
                vanilla["id"] = versionName;
                vanilla["forgeVersion"] = forge;
                var totalGameArgs = vanilla["arguments"]["game"].DeepClone().AsArray();
                if (forgeJson["arguments"] != null && forgeJson["arguments"]["game"] != null) { foreach (var gameArg in forgeJson["arguments"]["game"].AsArray()) { totalGameArgs.Add(gameArg.DeepClone()); } }
                var totalJvmArgs = vanilla["arguments"]["jvm"].DeepClone().AsArray();
                if (forgeJson["arguments"] != null && forgeJson["arguments"]["jvm"] != null) { foreach (var jvmArg in forgeJson["arguments"]["jvm"].AsArray()) { totalJvmArgs.Add(jvmArg.DeepClone()); } }

                vanilla["arguments"]["game"] = totalGameArgs.DeepClone();
                vanilla["arguments"]["jvm"] = totalJvmArgs.DeepClone();
                File.WriteAllText(Path.Combine(GamePath, "versions", versionName, versionName + ".json"), vanilla.ToJsonString(new JsonSerializerOptions{WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
            },"执行安装");
            return root;
        }
        private int SubTaskForFabric(string versionId, string versionName, string fabric, int taskId, int root, string fabricApi = "")
        {
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                var fabJsonUrl = $"{_downloadSource.FabricMeta}//v2/versions/loader/{versionId}/{fabric}/profile/json";
                var fabJson = await HttpUtils.GetString(fabJsonUrl);
                JsonNode node = JsonNode.Parse(fabJson);
                node["id"] = versionId;
                var fabricLibraries = DownloadTools.ParseLibraryFiles(JsonUtils.GetValueFromJson(fabJson, "libraries"));
                TaskManager.Values[taskId]["fabricLibs"] = fabricLibraries;
                var vanilla = JsonNode.Parse(File.ReadAllText(Path.Combine(GamePath, "versions", versionName, versionName + ".json")));
                
                vanilla["mainClass"] = node["mainClass"].DeepClone();
                var totalLibs = vanilla["libraries"].DeepClone().AsArray();
                foreach (var lib in node["libraries"].AsArray()) { totalLibs.Add(lib.DeepClone()); }
                vanilla["libraries"] = totalLibs.DeepClone();
                vanilla["id"] = versionName;
                vanilla["fabricVersion"] = fabric;
                var totalGameArgs = vanilla["arguments"]["game"].DeepClone().AsArray();
                if (node["arguments"] != null && node["arguments"]["game"] != null) { foreach (var gameArg in node["arguments"]["game"].AsArray()) { totalGameArgs.Add(gameArg.DeepClone()); } }
                var totalJvmArgs = vanilla["arguments"]["jvm"].DeepClone().AsArray();
                if (node["arguments"] != null && node["arguments"]["jvm"] != null) { foreach (var jvmArg in node["arguments"]["jvm"].AsArray()) { totalJvmArgs.Add(jvmArg.DeepClone()); } }

                vanilla["arguments"]["game"] = totalGameArgs.DeepClone();
                vanilla["arguments"]["jvm"] = totalJvmArgs.DeepClone();
                File.WriteAllText(Path.Combine(GamePath, "versions", versionName, versionName + ".json"), vanilla.ToJsonString(new JsonSerializerOptions{WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));

            }, "解析 Fabric 信息");
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                var libs = TaskManager.Values[taskId]["fabricLibs"] as List<LibraryFile>;
                var netFiles = DownloadTools.LibraryFilesToNetFiles(libs, GamePath);
                var backup = "";
                if (_downloadSource.SourceType == 0)
                {
                    var source = new DownloadSource();
                    source.Bmclapi();
                    backup = source.FabricMaven;
                    await DownloadFilesAsync(netFiles, _downloadSource.FabricMaven, backup);
                }
                else
                {
                    var source = new DownloadSource();
                    backup = source.FabricMaven;
                    await DownloadFilesAsync(netFiles, _downloadSource.FabricMaven, backup);
                }
            }, "下载 Fabric 依赖库");
            if (string.IsNullOrEmpty(fabricApi)) return root;
            TaskManager.Instance.AddSubTask(taskId, ++root, async ctx =>
            {
                //TODO: 下载Fabric API
            }, "下载 Fabric API");
            return root;
        }
        
        private int SubTaskForVanilla(string versionId, string versionName, int taskId, int root)
        {
                //版本ManiFest
            TaskManager.Instance.AddSubTask(taskId, ++root, async token =>
            {
                string manifest = await GetVersionManifest();
                var version = ParseManifest(versionId, manifest);
                TaskManager.Values.Add(taskId, new Dictionary<string, object>());
                TaskManager.Values[taskId]["version"] = version;
            }, "获取版本信息");
            //下载原版JSON
            TaskManager.Instance.AddSubTask(taskId, ++root, async token =>
            {
                DVersion version = TaskManager.Values[taskId]["version"] as DVersion;
                string path = $"{GamePath}/versions/{versionName}";
                Directory.CreateDirectory(path);
                string url = version.Url;
                string versionIndexJson = await HttpUtils.GetString(new Uri(url.Replace("https://piston-meta.mojang.com", _downloadSource.LauncherMeta)));
                var jsonNode = JsonNode.Parse(versionIndexJson);
                jsonNode["id"] = versionName;
                versionIndexJson = jsonNode.ToJsonString(new JsonSerializerOptions{WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
                File.Create($"{path}/{versionName}.json").Close();
                File.WriteAllText($"{path}/{versionName}.json", versionIndexJson);
            }, "准备下载");
            //依赖库和版本jaR
            TaskManager.Instance.AddSubTask(taskId, ++root, async token =>
            {
                Directory.CreateDirectory($"{GamePath}/libraries");
                string indexJson = File.ReadAllText($"{GamePath}/versions/{versionName}/{versionName}.json");
                TaskManager.Values[taskId]["indexJson"] = indexJson;
                string librariesStr = JsonUtils.GetValueFromJson(indexJson, "libraries");
                var libs = DownloadTools.ParseLibraryFiles(librariesStr);
                
                TaskManager.Values[taskId]["libs"] = libs;
            }, "解析依赖库信息");
            //下载
            TaskManager.Instance.AddSubTask(taskId, ++root, async token =>
            {
                var start = DateTime.Now;
                var libs = TaskManager.Values[taskId]["libs"] as List<LibraryFile>;
                string backup;
                var totalLibs = DownloadTools.LibraryFilesToNetFiles(libs, GameDownloader.GamePath);
                string indexJson = File.ReadAllText($"{GamePath}/versions/{versionName}/{versionName}.json");
                totalLibs.Add(new NetFile(){
                    Hash = JsonUtils.GetValueFromJson(indexJson, "downloads.client.sha1"),
                    Url = JsonUtils.GetValueFromJson(indexJson, "downloads.client.url"),
                    Path = $"{GamePath}/versions/{versionName}/{versionName}.jar"
                });
                if (_downloadSource.SourceType == 0)
                {
                    var source = new DownloadSource();
                    source.Bmclapi();
                    backup = source.Libraries;
                    await DownloadFilesAsync(totalLibs, _downloadSource.Libraries, backup);
                }
                else
                {
                    var source = new DownloadSource();
                    backup = source.Libraries;
                    await DownloadFilesAsync(totalLibs, _downloadSource.Libraries, backup);
                }

                var end = DateTime.Now;
                _logger.Info($"依赖库文件下载耗时{end.Subtract(start).TotalSeconds}s");
            }, "下载依赖库");
            //资源索引
            TaskManager.Instance.AddSubTask(taskId, ++root, async token =>
            {
                string indexJson = TaskManager.Values[taskId]["indexJson"] as string;
                string url = JsonUtils.GetValueFromJson(indexJson, "assetIndex.url");
                string sha1 = JsonUtils.GetValueFromJson(indexJson, "assetIndex.sha1");
                string path = $"{GamePath}/assets/indexes/{JsonUtils.GetValueFromJson(indexJson, "assetIndex.id")}.json";
                if ((await CalculateFileSHA1(path)).ToLower() != sha1.ToLower())
                {
                    var indexFile = new NetFile(){
                        Url = url,
                        Hash = sha1,
                        Path = path
                    };

                    await DownloadFileAsync(indexFile);                    
                }
                
                var assets = DownloadTools.ParseAssetObjects(File.ReadAllText(path), _downloadSource, GamePath);
                
                TaskManager.Values[taskId]["assets"] = assets;
            }, "解析资源索引文件");
            TaskManager.Instance.AddSubTask(taskId, ++root, async token =>
            {
                var assets = TaskManager.Values[taskId]["assets"] as List<NetFile>;
                var start = DateTime.Now;
                string backup;
                if (_downloadSource.SourceType == 0)
                {
                    var source = new DownloadSource();
                    source.Bmclapi();
                    backup = source.ResourcesDownload;
                    await DownloadFilesAsync(assets, _downloadSource.ResourcesDownload, backup).ConfigureAwait(false);
                }
                else
                {
                    var source = new DownloadSource();
                    backup = source.ResourcesDownload;
                    await DownloadFilesAsync(assets, _downloadSource.ResourcesDownload, backup).ConfigureAwait(false);
                }

                var end = DateTime.Now;

                _logger.Info($"资源文件下载耗时{end.Subtract(start).TotalSeconds}s");
                _logger.Info($"版本{versionName}:{versionId}下载已完成");
            }, "下载资源文件");
            return root;
        }
        
        #endregion
        
        private CancellationTokenSource _tokenSource = new CancellationTokenSource();

        public async Task DownloadFilesAsync(List<NetFile> files, string defHost = "", string backupHost = "", string currentRoot = "",
            List<string> cachedRoots = null)
        {
            int pid = new Random().Next(1000, 9999);
            _logger.Info($"{pid}下载任务进度 0/{files.Count}");

            bool backup = !string.IsNullOrEmpty(backupHost);
            //url:[paths]
            cachedRoots ??= new List<string>();
            bool isBackup = false;
            using (var semaphore = new SemaphoreSlim(150))
            {
                var tasks = new List<Task>();

                int doneCount = 0;
                int completedCount = 0;

                foreach (var f in files)
                {
                    await semaphore.WaitAsync();
                    if (_tokenSource.IsCancellationRequested)
                    {
                        _logger.Info($"{pid} 取消下载任务");
                        return;
                    }

//                if(ctx.IsCancellationRequested) return;
                    tasks.Add(Task.Run(async () =>
                    {
                        semaphore.Release();
                        Interlocked.Increment(ref completedCount);

                        await DownloadFileAsync(f, defHost, backupHost, currentRoot,cachedRoots, pid);
                        // 在一定间隔内触发垃圾回收
                        if (completedCount >= 50)
                        {
//                            _logger.Debug($"Triggering garbage collection after {completedCount} downloads...");
                            Interlocked.Exchange(ref completedCount, 0);
//                            GC.Collect();
//                            GC.WaitForPendingFinalizers();
                        }
                        Interlocked.Increment(ref doneCount);
                        if (doneCount % 500 == 0)
                        {
                            _logger.Info($"{pid}下载任务进度 {doneCount}/{files.Count}");
                        }
  //                      GC.Collect();
  //                      GC.WaitForPendingFinalizers();
                    }));
                }
                
                await Task.WhenAll(tasks);
                _logger.Info($"{pid}下载任务进度 {doneCount}/{files.Count}");
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public async Task DownloadFileAsync(NetFile f, string defHost = "", string backupHost = "",string currentRoot = "", List<string> cachedRoots = null, int pid = 0)
        {
            cachedRoots ??= new List<string>();
            foreach (var root in cachedRoots)
            {
                if (await CalculateFileSHA1(f.Path.Replace(currentRoot, root)) == f.Hash)
                {
                    File.Copy(f.Path.Replace(currentRoot, root), f.Path, true);
                }
            }
            bool backup = !string.IsNullOrEmpty(backupHost);
            bool isBackup = false;
            var sha1 = f.Hash;
            var path = f.Path;
            var url = f.Url;
            string backurl = null;
            if (backup)
            {
                backurl = url.Replace(defHost, backupHost);
            }

            int i = 0;
            while (i <= 10)
            {
                if (_tokenSource.IsCancellationRequested)
                {
                    _logger.Info($"{pid} 取消下载任务");
                    return;
                }

                i++;
                try
                {
                    if (File.Exists(path))
                    {
                        var s = await CalculateFileSHA1(path);
                        if (s == sha1 || string.IsNullOrEmpty(sha1))
                        {
                            break;
                        }

                        File.Delete(path);
                    }

                    Downloader downloader = new Downloader(url, path);
                    downloader.Timeout = TimeSpan.FromSeconds(60);
                    await downloader.DownloadFileAsync();
                    downloader.Dispose();
                    if (File.Exists(path))
                    {
                        var s = await CalculateFileSHA1(path);
                        if (s == sha1 || string.IsNullOrEmpty(sha1))
                        {
                            break;
                        }

                        File.Delete(path);
                    }
                }
                catch (TimeoutException ex)
                {
                    _logger.Warn($"{pid}下载任务遇到超时：{ex.Message} | D:{url} - {path} - {sha1}");
                    if (isBackup) break;
                    if (backup)
                    {
                        url = backurl;
                        _logger.Warn($"{pid}下载任务正在切换至备用源");
                    }

                    isBackup = true;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("429"))
                    {
                        await Task.Delay(200);
                        _logger.Warn($"{pid}下载任务遇到429：{ex.Message} | D:{url} - {path} - {sha1}");
                        if (!isBackup && new Random().Next(0, 666) >= 333)
                        {
                            if (backup)
                            {
                                _logger.Warn($"{pid}下载任务正在切换至备用源");
                                url = backurl;
                                isBackup = true;
                            }
                        }

                        continue;
                    }

                    if (ex.Message.Contains("404"))
                    {
                        _logger.Warn($"{pid}下载任务遇到404：{ex.Message} | D:{url} - {path} - {sha1}");
                        if (isBackup)
                        {
                            throw new FileNotFoundException("下载时远程服务器无法找到该文件，已尝试备用源。", url);
                        }
                        await Task.Delay(200);
                        if (backup)
                        {
                            _logger.Warn($"{pid}下载任务正在切换至备用源");
                            url = backurl;
                            isBackup = true;
                        }
                        else
                        {
                            throw new FileNotFoundException("下载时远程服务器无法找到该文件，无备用源。", url);
                            break;
                        }
                    }
                }
            }

        }

        public DVersion ParseManifest(string versionId, string manifest)
        {
            var document = JsonDocument.Parse(manifest);
            var versions = document.RootElement.GetProperty("versions");
            foreach (var version in versions.EnumerateArray())
            {
                string id = version.GetProperty("id").GetString();
                string type = version.GetProperty("type").GetString();
                string url = version.GetProperty("url").GetString();
                string time = version.GetProperty("time").GetString();
                string releaseTime = version.GetProperty("releaseTime").GetString();
                if (id != versionId) continue;
                DVersion verd;
                if (type.Equals("snapshot"))
                {
                    verd = new DVersion(id, 1, url, time, releaseTime);
                }
                else if (type.Equals("release"))
                {
                    verd = new DVersion(id, 0, url, time, releaseTime);
                }
                else if (type.Equals("old_alpha"))
                {
                    verd = new DVersion(id, 3, url, time, releaseTime);
                }
                else if (type.Equals("old_beta"))
                {
                    verd = new DVersion(id, 2, url, time, releaseTime);
                }
                else
                {
                    _logger.Warn("Found new unknown version type: " + type);
                    continue;
                }

                return verd;
            }

            return null;

        }

        public (List<DVersion> normal, List<DVersion> alpha, List<DVersion> beta) ParseManifest(string manifest)
        {
            try
            {
                var document = JsonDocument.Parse(manifest);
                var normal = new List<DVersion>();
                var alpha = new List<DVersion>();
                var beta = new List<DVersion>();
                var latest = document.RootElement.GetProperty("latest");
                // Parse versions
                var versions = document.RootElement.GetProperty("versions");
                foreach (var version in versions.EnumerateArray())
                {
                    string id = version.GetProperty("id").GetString();
                    string type = version.GetProperty("type").GetString();
                    string url = version.GetProperty("url").GetString();
                    string time = version.GetProperty("time").GetString();
                    string releaseTime = version.GetProperty("releaseTime").GetString();
                    DVersion verd;
                    if (type.Equals("snapshot"))
                    {
                        verd = new DVersion(id, 1, url, time, releaseTime);
                        normal.Add(verd);
                    }
                    else if (type.Equals("release"))
                    {
                        verd = new DVersion(id, 0, url, time, releaseTime);
                        normal.Add(verd);
                    }
                    else if (type.Equals("old_alpha"))
                    {
                        verd = new DVersion(id, 3, url, time, releaseTime);
                        alpha.Add(verd);
                    }
                    else if (type.Equals("old_beta"))
                    {
                        verd = new DVersion(id, 2, url, time, releaseTime);
                        beta.Add(verd);
                    }
                    else
                    {
                        _logger.Warn("Found new unknown version type: " + type);
                    }
                }

                return (normal, alpha, beta);
            }
            catch (Exception e)
            {
                Console.Write(e.ToString());
                throw e;
            }

        }

        public async Task<(List<string> forges, List<string> fabs, List<string> opts)> GetForgeFabricOptifineVersionList(string mcVersion)
        {
            //forge
            string json = await HttpUtils.GetString(_downloadSource.ForgeSupportedMc);
            var document = JsonDocument.Parse(json);
            var element = document.RootElement;
            string[] versions = JsonSerializer.Deserialize<string[]>(json);
            var forges = new List<string>();
            if (Array.Exists(versions, version => version == mcVersion))
            {
                json = await HttpUtils.GetString(_downloadSource.ForgeListViaMcVer.Replace("{mcversion}", mcVersion));
                document = JsonDocument.Parse(json);
                foreach (var forgeVer in document.RootElement.EnumerateArray()) forges.Add(forgeVer.GetProperty("version").GetString());
                forges.Sort((v1, v2) => CompareVersions(v2, v1));
            }

            //optifine
            json = await HttpUtils.GetString(_downloadSource.OptifineListViaMcVer.Replace("{mcversion}", mcVersion));
            var opts = new List<string>();
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(json.Trim()) || string.IsNullOrEmpty(json.Trim('[', ']', '{', '}', ' ')) ||
                string.IsNullOrEmpty(json.Trim('[', ']', '{', '}', ' ')))
            {
//                opts.Add("N");
            }
            else
            {
                document = JsonDocument.Parse(json);
                foreach (var optVer in document.RootElement.EnumerateArray())
                {
                    string patch = optVer.GetProperty("patch").GetString();
                    opts.Insert(0, patch);
                }
            }

            //fabric
            var fabs = new List<string>();
            json = await HttpUtils.GetString(_downloadSource.FabricManifest);
            document = JsonDocument.Parse(json);
            bool isEnable = false;
            foreach (var el in document.RootElement.GetProperty("game").EnumerateArray())
            {
                string version = el.GetProperty("version").GetString();
                if (version.Equals(mcVersion))
                {
                    isEnable = true;
                    break;
                }
            }

            if (isEnable)
            {
                foreach (var el in document.RootElement.GetProperty("loader").EnumerateArray())
                {
                    string version = el.GetProperty("version").GetString();
                    fabs.Add(version);
                }
            }

            return (forges, fabs, opts);
        }

        private static int CompareVersions(string version1, string version2)
        {
            string[] parts1 = version1.Split('.');
            string[] parts2 = version2.Split('.');
            int maxLength = Math.Max(parts1.Length, parts2.Length);

            for (int i = 0; i < maxLength; i++)
            {
                int v1 = i < parts1.Length ? int.Parse(parts1[i]) : 0;
                int v2 = i < parts2.Length ? int.Parse(parts2[i]) : 0;

                if (v1 != v2)
                {
                    return v1.CompareTo(v2);
                }
            }

            return 0;
        }
    }
}