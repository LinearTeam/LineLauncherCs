using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using LMC.Basic;
using LMC.Tasks;
using LMC.Utils;

namespace LMC.Minecraft
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
        
        public GameDownloader(){}
        
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
            string forge = "")
        {
            if (!(isOptifine || isFabric || isForge))
            {
                _logger.Info($"新的原版游戏下载任务: {versionId} , 名称: {versionName}");
                await DownloadGame(versionId, versionName);
            }
        }

        private async Task DownloadGame(string versionId, string versionName)
        {
            var task = TaskManager.Instance.CreateTask(5, $"下载原版游戏 {versionName} ({versionId})");
            _tokenSource = task.CancellationTokenSource;
            //版本ManiFest
            TaskManager.Instance.AddSubTask(task.Id, 0, async token =>
            {
                string manifest = await GetVersionManifest();
                var version = ParseManifest(versionId, manifest);
                TaskManager.Values.Add(task.Id, new Dictionary<string, object>());
                TaskManager.Values[task.Id]["version"] = version;
            }, "获取版本信息"); 
            //下载原版JSON
            TaskManager.Instance.AddSubTask(task.Id, 1, async token =>
            {
                DVersion version = TaskManager.Values[task.Id]["version"] as DVersion;
                string path = $"{GamePath}/versions/{versionName}";
                Directory.CreateDirectory(path);
                string url = version.Url;
                string versionIndexJson = await HttpUtils.GetString(new Uri(url.Replace("https://piston-meta.mojang.com", _downloadSource.LauncherMeta)));
                var jsonNode = JsonNode.Parse(versionIndexJson);
                jsonNode["id"] = versionName;
                versionIndexJson = jsonNode.ToJsonString(new JsonSerializerOptions{WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
                File.Create($"{path}/{versionName}.json").Close();
                File.WriteAllText($"{path}/{versionName}.json", versionIndexJson);
                Directory.CreateDirectory($"{path}/LMC/");
                File.Create($"{path}/LMC/version.line").Close();
                var lineFileParser = new LineFileParser();
                lineFileParser.Write($"{path}/LMC/version.line", "name", versionName, "base");
                lineFileParser.Write($"{path}/LMC/version.line", "isModPack", "N", "modpack");
                lineFileParser.Write($"{path}/LMC/version.line", "Fabric", "N", "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "Forge", "N", "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "Optifine", "N", "loader");
                lineFileParser.Write($"{path}/LMC/version.line", "id", versionId, "base");
            }, "准备下载");
            //依赖库和版本jaR
            TaskManager.Instance.AddSubTask(task.Id, 2, async token =>
            {
                Directory.CreateDirectory($"{GamePath}/libraries");
                string indexJson = File.ReadAllText($"{GamePath}/versions/{versionName}/{versionName}.json");
                TaskManager.Values[task.Id]["indexJson"] = indexJson;
                string librariesStr = JsonUtils.GetValueFromJson(indexJson, "libraries");
                var larr = JsonNode.Parse(librariesStr).AsArray();
                var libs = new Dictionary<string, string>();
                foreach (var lib in larr)
                {
                    string url, path;
                    string libStr = lib.ToJsonString();
                    if (JsonUtils.GetValueFromJson(libStr, "downloads.artifact") != null)
                    {
                        url = JsonUtils.GetValueFromJson(libStr, "downloads.artifact.url").Replace("https://libraries.minecraft.net", _downloadSource.Libraries);
                        path =
                            $"{GamePath}/libraries/{JsonUtils.GetValueFromJson(libStr, "downloads.artifact.path")}|{JsonUtils.GetValueFromJson(libStr, "downloads.artifact.sha1")}";
                        try
                        {
                            libs.Add(url, path);
                        }
                        catch
                        {
                            _logger.Warn("Failed to add " + url + " to libs dict");
                        }
                    }

                    if (JsonUtils.GetValueFromJson(libStr, "downloads.natives.windows") != null)
                    {
                        libStr = JsonUtils.GetValueFromJson(libStr, $"downloads.classifiers.{JsonUtils.GetValueFromJson(libStr, "downloads.natives.windows")}");
                        url = JsonUtils.GetValueFromJson(libStr, "url").Replace("https://libraries.minecraft.net", _downloadSource.Libraries);
                        path = $"{GamePath}/libraries/{JsonUtils.GetValueFromJson(libStr, "path")}|{JsonUtils.GetValueFromJson(libStr, "sha1")}";
                        try
                        {
                            libs.Add(url, path);
                        }
                        catch
                        {
                            
                        }
                    }

                }
                libs.Add(JsonUtils.GetValueFromJson(indexJson, "downloads.client.url"),
                    $"{GamePath}/versions/{versionName}/{versionName}.jar|{JsonUtils.GetValueFromJson(indexJson, "downloads.client.sha1")}");
                TaskManager.Values[task.Id]["libs"] = libs;
            }, "解析依赖库信息");
            //下载
            TaskManager.Instance.AddSubTask(task.Id, 3, async token =>
            {
                var start = DateTime.Now;
                Dictionary<string, string> libs = TaskManager.Values[task.Id]["libs"] as Dictionary<string, string>;
                string backup;
                if (_downloadSource.SourceType == 0)
                {
                    var source = new DownloadSource();
                    source.Bmclapi();
                    backup = source.Libraries;
                    await DownloadFilesWithHashAsync(libs, _downloadSource.Libraries, backup);
                }
                else
                {
                    var source = new DownloadSource();
                    backup = source.Libraries;
                    await DownloadFilesWithHashAsync(libs);
                }
                var end = DateTime.Now;
                _logger.Info($"依赖库文件下载耗时{end.Subtract(start).TotalSeconds}s");
            }, "下载依赖库");
            //资源索引
            TaskManager.Instance.AddSubTask(task.Id, 4, async token =>
            {
                Dictionary<string, string> libs = TaskManager.Values[task.Id]["libs"] as Dictionary<string, string>;
                string indexJson = TaskManager.Values[task.Id]["indexJson"] as string;
                string url = JsonUtils.GetValueFromJson(indexJson, "assetIndex.url");
                string sha1 = JsonUtils.GetValueFromJson(indexJson, "assetIndex.sha1");
                string path = $"{GamePath}/assets/indexes/{JsonUtils.GetValueFromJson(indexJson, "assetIndex.id")}.json";
                if ((await CalculateFileSHA1(path)).ToLower() != sha1.ToLower())
                {
                    libs.Clear();
                    libs.Add(url.Replace("https://piston-meta.mojang.com", _downloadSource.LauncherMeta), path + "|" + sha1);
                }

                await DownloadFilesWithHashAsync(libs);
                
                
                string objects = JsonUtils.GetValueFromJson(File.ReadAllText(path), "objects");
                var assets = new Dictionary<string, string>();
                await Task.Run(() =>
                {
                    using (var doc = JsonDocument.Parse(objects))
                    {
                        var root = doc.RootElement;
                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            var verifiedFiles = new List<string>();
                            int parsedFiles = 0;
                            foreach (var property in root.EnumerateObject())
                            {
                                string hash = JsonUtils.GetValueFromJson(property.Value.ToString(), "hash");
                                url = $"{_downloadSource.ResourcesDownload}/{hash.Substring(0, 2)}/{hash}";
                                path = $"{GamePath}/assets/objects/{hash.Substring(0, 2)}/{hash}|{hash}";
                                try
                                {
                                    assets.Add(url, path);
                                }
                                catch
                                {
                                    _logger.Warn("无法将" + url + " 添加到资源字典 ");
                                }

                                parsedFiles++;
                            }
                        }
                    }
                });
                
                TaskManager.Values[task.Id]["assets"] = assets;
            }, "解析资源索引文件");
            TaskManager.Instance.AddSubTask(task.Id, 5, async token =>
            {
                Dictionary<string, string> assets = TaskManager.Values[task.Id]["assets"] as Dictionary<string, string>;
                var start = DateTime.Now;
                string backup;
                if (_downloadSource.SourceType == 0)
                {
                    var source = new DownloadSource();
                    source.Bmclapi();
                    backup = source.ResourcesDownload;
                    await DownloadFilesWithHashAsync(assets,_downloadSource.ResourcesDownload, backup);
                }
                else
                {
                    var source = new DownloadSource();
                    backup = source.ResourcesDownload;
                    await DownloadFilesWithHashAsync(assets, _downloadSource.ResourcesDownload, backup);
                }

                var end = DateTime.Now;

                _logger.Info($"资源文件下载耗时{end.Subtract(start).TotalSeconds}s");
                _logger.Info($"版本{versionName}:{versionId}下载已完成");
            }, "下载资源文件");
            task.Status = ExecutionStatus.Waiting;
            TaskManager.Instance.ExecuteTasksAsync();
        }

        private  CancellationTokenSource _tokenSource = new CancellationTokenSource();
        public async Task DownloadFilesWithHashAsync(Dictionary<string, string> urlPathDictionary, string defHost = "", string backupHost = "",
            Dictionary<string, List<string>> cachedFiles = null)
        {
            int pid = new Random().Next(1000, 9999);
            _logger.Info($"{pid}下载任务进度 0/{urlPathDictionary.Count}");

            bool backup = !string.IsNullOrEmpty(backupHost);
            //url:[paths]
            cachedFiles ??= new Dictionary<string, List<string>>();
            bool isBackup = false;
            using (var semaphore = new SemaphoreSlim(150))
            {
                var tasks = new List<Task>();
                
                int doneCount = 0;
                int completedCount = 0;

                foreach (var kvp in urlPathDictionary)
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
                        var sha1 = kvp.Value.Split('|')[1];
                        var path = kvp.Value.Split('|')[0];
                        var url = kvp.Key;
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
                                    if (s == sha1)
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
                                    if (s == sha1)
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
                                    if (isBackup) break;
                                    await Task.Delay(200);
                                    if (backup)
                                    {
                                        _logger.Warn($"{pid}下载任务正在切换至备用源");
                                        url = backurl;
                                        isBackup = true;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                        }

                        semaphore.Release();
                        Interlocked.Increment(ref completedCount);

                        // 在一定间隔内触发垃圾回收
                        if (completedCount >= 50)
                        {
                            Console.WriteLine($"Triggering garbage collection after {completedCount} downloads...");
                            Interlocked.Exchange(ref completedCount, 0);
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                        Interlocked.Increment(ref doneCount);
                        if (doneCount % 500 == 0)
                        {
                            _logger.Info($"{pid}下载任务进度 {doneCount}/{urlPathDictionary.Count}");
                        }
                    }));
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.WhenAll(tasks);
                _logger.Info($"{pid}下载任务进度 {doneCount}/{urlPathDictionary.Count}");
                GC.Collect();
                GC.WaitForPendingFinalizers();
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