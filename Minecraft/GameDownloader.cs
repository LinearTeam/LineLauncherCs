using Neon.Downloader;
using LMC.Account.OAuth;
using LMC.Basic;
using LMC.Pages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Linq;
using static LMC.Account.OAuth.OAuth;
using Common;
using LMC.Utils;

namespace LMC.Minecraft
{
    /*
     * GameDownload Tools Class
     */
    public class GameDownloader
    {
        public static String GamePath = @"./.minecraft";
        private static HttpClient s_client = new HttpClient();
        private DownloadSource _downloadSource = new DownloadSource();
        private Logger _logger = new Logger("GD");

        public void ChangeSource(DownloadSource newSource)
        {
            if (newSource == _downloadSource || newSource == null) return;
            this._downloadSource = newSource;
        }

        public void Bmclapi()
        {
            _downloadSource.Bmclapi();
        }

        public void LineMirror()
        {
            _downloadSource.LineMirror();
        }

        async public Task<String> GetVersionManifest()
        {
            return await s_client.GetStringAsync(_downloadSource.VersionManifest);
        }


        public async Task<string> CalculateFileSHA1(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (FileStream fileStream = File.OpenRead(filePath))
                    {
                        using (SHA1 sha1 = SHA1.Create())
                        {
                            // 计算哈希值
                            byte[] hashBytes = sha1.ComputeHash(fileStream);

                            // 将哈希字节数组转换为十六进制字符串
                            StringBuilder sb = new StringBuilder();
                            foreach (byte b in hashBytes)
                            {
                                sb.Append(b.ToString("x2"));
                            }
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

        public async Task DownloadGame(string versionId, string versionName, bool isOptifine, bool isFabric, bool isForge, string optifine = "", string fabric = "", string forge = "")
        {
            if (MainWindow.RunningTask) {
                await MainWindow.ShowMsgBox("提示", "当前正在进行其他任务，请等待完成后继续。","确定");
                return;
            }
            MainWindow.RunningTask = true;
            if (!(isOptifine || isFabric || isForge))
            {
                int id = new Random().Next(1000,9999);
                _logger.Info($"新的原版游戏下载任务: {versionId} , 名称: {versionName}, pid:{id}");
                await DownloadGame(versionId, versionName,id);
            }
            MainWindow.RunningTask = false;
        }

        async private Task DownloadGame(string versionId, string versionName,int pid)
        {
            MainWindow.MainNagView.Navigate(typeof(DownloadingPage));
            DownloadingPage.ProProc = "当前进行中：    下载原版Json";
            DownloadingPage.ProProg = "当前任务进度：  0%";
            DownloadingPage.ProgressRing.IsIndeterminate = true;
            _logger.Info("获取manifest" + " pid:" + pid);
            string manifest = await GetVersionManifest();
            DownloadingPage.ProProg = "当前任务进度：  30%";
            _logger.Info("解析manifest" + " pid:" + pid);
            DVersion version = ParseManifest(versionId, manifest);
            string path = $"{GamePath}/versions/{versionName}";
            Directory.CreateDirectory(path);
            //Download Version Json
            _logger.Info("下载原版json" + " pid:" + pid);
            DownloadingPage.ProProg = "当前任务进度：  70%";
            string url = version.Url;
            _logger.Info("1" + " pid:" + pid); 
            string versionIndexJson = await s_client.GetStringAsync(new Uri(url.Replace("https://piston-meta.mojang.com", _downloadSource.LauncherMeta)));
            _logger.Info("2" + " pid:" + pid);
            JsonNode? jsonNode = JsonNode.Parse(versionIndexJson);
            jsonNode["id"] = versionName;
            versionIndexJson = jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            File.Create($"{path}/{versionName}.json").Close();
            File.WriteAllText($"{path}/{versionName}.json", versionIndexJson);
            Directory.CreateDirectory($"{path}/LMC/");
            File.Create($"{path}/LMC/version.line").Close();
            DownloadingPage.ProProg = "当前任务进度：  90%";
            LineFileParser lineFileParser = new LineFileParser();
            lineFileParser.Write($"{path}/LMC/version.line", "name", versionName, "base");
            lineFileParser.Write($"{path}/LMC/version.line", "isModPack", "N", "modpack");
            lineFileParser.Write($"{path}/LMC/version.line", "Fabric", "N", "loader");
            lineFileParser.Write($"{path}/LMC/version.line", "Forge", "N", "loader");
            lineFileParser.Write($"{path}/LMC/version.line", "Optifine", "N", "loader");
            lineFileParser.Write($"{path}/LMC/version.line", "id", versionId, "base");
            DownloadingPage.ProProg = "当前任务进度：  100% 已完成";
            _logger.Info("3" + " pid:" + pid);
            //Download libs and version jar
            DownloadingPage.ProProc = "当前进行中：    下载依赖库";
            DownloadingPage.ProProg = "当前任务进度：  0%";
            Directory.CreateDirectory($"{GamePath}/libraries");
            _logger.Info("4" + " pid:" + pid);
            string indexJson = File.ReadAllText($"{GamePath}/versions/{versionName}/{versionName}.json");
            string librariesStr = GetValueFromJson(indexJson, "libraries");
            JsonArray larr = JsonArray.Parse(librariesStr).AsArray();
            Dictionary<string, string> libs = new Dictionary<string, string>();
            foreach (var lib in larr)
            {
                string libStr = lib.ToJsonString();
                if (GetValueFromJson(libStr, "downloads.artifact") != null)
                {
                    url = GetValueFromJson(libStr, "downloads.artifact.url").Replace("https://libraries.minecraft.net", _downloadSource.Libraries);
                    path = $"{GamePath}/libraries/{GetValueFromJson(libStr, "downloads.artifact.path")}|{GetValueFromJson(libStr, "downloads.artifact.sha1")}";
                    try { libs.Add(url, path); } catch { _logger.Warn("Failed to add " + url + " to libs dict" + " pid:" + pid); }
                }
                if (GetValueFromJson(libStr, "downloads.natives.windows") != null)
                {
                    libStr = GetValueFromJson(libStr, $"downloads.classifiers.{GetValueFromJson(libStr, "downloads.natives.windows")}");
                    url = GetValueFromJson(libStr, "url").Replace("https://libraries.minecraft.net", _downloadSource.Libraries);
                    path = $"{GamePath}/libraries/{GetValueFromJson(libStr, "path")}|{GetValueFromJson(libStr, "sha1")}";
                    try { libs.Add(url, path); } catch { _logger.Warn("Failed to add " + url + " to libs dict" + " pid:" + pid); }
                }

            }
            _logger.Info("5" + " pid:" + pid); 
            libs.Add(GetValueFromJson(indexJson, "downloads.client.url"), $"{GamePath}/versions/{versionName}/{versionName}.jar|{GetValueFromJson(indexJson, "downloads.client.sha1")}");
            string backup;
            if (_downloadSource.SourceType == 0)
            {
                var source = new DownloadSource();
                source.Bmclapi();
                backup = source.Libraries;
                await DownloadFilesAsync(libs, pid, _downloadSource.Libraries, backup);
            }
            else {
                var source = new DownloadSource();
                backup = source.Libraries;
                await DownloadFilesAsync(libs, pid);
            }
            _logger.Info("6");
            DownloadingPage.ProProg = "当前任务进度：  100% 已完成";

            //Assets Index
            DownloadingPage.ProProc = "当前进行中：    下载资源索引";
            DownloadingPage.ProProg = "当前任务进度：  0%";
            url = GetValueFromJson(indexJson, "assetIndex.url");
            string sha1 = GetValueFromJson(indexJson, "assetIndex.url");
            path = $"{GamePath}/assets/indexes/{GetValueFromJson(indexJson, "assetIndex.id")}.json";
            if((await CalculateFileSHA1(path)).ToLower() != sha1.ToLower())
            {
                libs.Clear();
                libs.Add(url.Replace("https://piston-meta.mojang.com", _downloadSource.LauncherMeta), path + "|" + sha1);
            }
            await DownloadFilesAsync(libs , pid);
            DownloadingPage.ProProg = "当前任务进度：  100% 已完成";

            //Assets Object
            DownloadingPage.ProProc = "当前进行中：    解析资源文件";
            DownloadingPage.ProProg = "当前任务进度：  0%";

            string objects = GetValueFromJson(File.ReadAllText(path), "objects");
            Dictionary<string, string> assets = new Dictionary<string, string>();
            await Task.Run(() =>
            {
                using (JsonDocument doc = JsonDocument.Parse(objects))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        List<string> verifiedFiles = new List<string>();
                        int parsedFiles = 0;
                        foreach (JsonProperty property in root.EnumerateObject())
                        {
                            string hash = GetValueFromJson(property.Value.ToString(), "hash");
                            url = $"{_downloadSource.ResourcesDownload}/{hash.Substring(0, 2)}/{hash}";
                            path = $"{GamePath}/assets/objects/{hash.Substring(0, 2)}/{hash}|{hash}";
                            try { assets.Add(url, path); } catch { _logger.Warn("无法将" + url + " 添加到资源字典 " + " pid:" + pid); }
                            parsedFiles++;
                            DownloadingPage.ProProg = $"当前任务进度：    {((double)parsedFiles / root.EnumerateObject().ToArray().Length * 100):0.00}%";
                        }
                    }
                }
            });
            DownloadingPage.ProProg = "当前任务进度：  100% 已完成";
            DownloadingPage.ProProc = "当前进行中：    下载资源文件";
            DownloadingPage.ProProg = "当前任务进度：  0%";

            var start = DateTime.Now; 

            if (_downloadSource.SourceType == 0)
            {
                var source = new DownloadSource();
                source.Bmclapi();
                backup = source.ResourcesDownload;
                await DownloadFilesAsync(assets, pid, _downloadSource.ResourcesDownload, backup);
            }
            else
            {
                var source = new DownloadSource();
                backup = source.ResourcesDownload;
                await DownloadFilesAsync(assets, pid, _downloadSource.ResourcesDownload, backup);
            }

            var end = DateTime.Now;
            
            _logger.Info($"资源文件下载耗时{end.Subtract(start).TotalSeconds}s" + " pid:" + pid);
            DownloadingPage.ProProc = "当前进行中：    下载版本 " + versionName;
            DownloadingPage.ProProg = "当前任务进度：  100% 已完成";
            DownloadingPage.ProgressRing.IsIndeterminate = false;
            _logger.Info($"版本{versionName}:{versionId}下载已完成");
            await MainWindow.ShowMsgBox("提示",$"版本 {versionName} 下载已完成。","确定");
        }
        private int _remainingFiles; 
        private int _totalFiles; 
        private int _maxRetries = 30;
        public async Task DownloadFilesAsync(Dictionary<string, string> urlPathDictionary, int pid, string defHost = "", string backupHost = "", Dictionary<string,List<string>> cachedFiles = null)
        {
            _remainingFiles = urlPathDictionary.Count;
            _totalFiles = urlPathDictionary.Count;
            _debug.Clear(); 
            var task = Task.Run(async () =>
            { 
                foreach(var kvp in urlPathDictionary)
                {
                    var url = kvp.Key;
                    var path = kvp.Value;
                    if (cachedFiles != null)
                    {
                        await DownloadFileAsync(url, path, pid, defHost, backupHost, cachedFiles[url]);
                    }
                    await DownloadFileAsync(url, path, pid, defHost, backupHost);
                }
            });
            await task;
            while (true)
            {
                if (_remainingFiles <= 0) break;    
                _logger.Info($"剩余文件 {_remainingFiles} / {_totalFiles}" + " pid:" + pid);
                if(_remainingFiles < 10)
                {
                    try
                    {
                        _debug.ForEach((kvp) =>
                        {
                            _logger.Info("剩余文件：" + kvp.Key + "  " + kvp.Value + " pid:" + pid);
                        });
                    }
                    catch(Exception e) {
                        _logger.Info(_debug.ToString() + " pid:" + pid);
                        _debug.Clear();
                    }
                }
                await Task.Delay(2000);
            }
        }
        private Dictionary<string, int> _debug = new Dictionary<string, int>();
        private async Task DownloadFileAsync(string url, string filePath,int pid , string defHost = "", string backupHost = "", List<string> cacheFiles = null)
        {
//            GC.Collect();
            int attempt = 0;
            string sha = filePath.Split('|')[1];
            filePath = filePath.Split('|')[0];
            if (cacheFiles != null)
            {
                foreach (string s in cacheFiles)
                {
                    if (File.Exists(s) && ((await CalculateFileSHA1(s)).ToLower() == sha.ToLower()))
                    {
                        File.Copy(s, filePath);
                        Interlocked.Decrement(ref _remainingFiles);
                        DownloadingPage.ProProg = $"当前任务进度：    {(((double)(_totalFiles - _remainingFiles)) / _totalFiles * 100):0.00}%";
                        return;
                    }
                }
            }
            while (true) {
                try { _debug.Add(url, pid); } catch { }
                if (attempt >= _maxRetries)
                {
                    throw new WebException("Failed to download " + url + " to " + filePath + " after " + attempt + " retries." + " pid:" + pid);
                }
                attempt++;
                try
                {
                    if(File.Exists(filePath))
                    {
                        string fileSha1 = await CalculateFileSHA1(filePath);
                        if(sha.ToLower() == fileSha1.ToLower())
                        {
                            Interlocked.Decrement(ref _remainingFiles);
                            DownloadingPage.ProProg = $"当前任务进度：    {(((double)(_totalFiles - _remainingFiles)) / _totalFiles * 100):0.00}%";
                            return;
                        }
                    }
                    /*Directory.CreateDirectory(Directory.GetParent(filePath).FullName);
                    s_httpClient.DefaultRequestHeaders.Add("User-Agent", $"LMC/C{MainWindow.LauncherVersion}");
                    var response = await s_httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    using (FileStream fs = File.OpenWrite(filePath))
                    {
                        await response.Content.CopyToAsync(fs);
                    }*/
                    /*                    var request = (HttpWebRequest) HttpWebRequest.Create(url);
                                        request.Method = "GET";
                                        request.UserAgent = $"LMC/C{MainWindow.LauncherVersion}";
                                        request.KeepAlive = false;
                                        request.Timeout = 3000;
                                        using (var response = await request.GetResponseAsync())
                                        using (Stream sr = response.GetResponseStream())
                                        using (FileStream fs = File.Create(filePath) )
                                        {
                                            byte[] buffer = new byte[8192000];
                                            int read;
                                            while((read = sr.Read(buffer, 0, buffer.Length)) > 0)
                                            {
                                                await fs.WriteAsync(buffer, 0, read);
                                            }
                                        }
                      */
                    Downloader downloader = new Downloader(url, filePath);
                    downloader.Timeout = TimeSpan.FromSeconds(20);
                    await downloader.DownloadFileAsync();
                    downloader.Dispose();
                    downloader = null;
                    Interlocked.Decrement(ref _remainingFiles);
                    DownloadingPage.ProProg = $"当前任务进度：    {(((double)(_totalFiles - _remainingFiles)) / _totalFiles * 100):0.00}%";
                    _debug.Remove(url);
                    GC.Collect();
                    break;
                }
                catch(TimeoutException ex)
                {
                    _logger.Warn("下载文件" + url + "超时：" + ex.Message + " pid:" + pid);
                    while (true)
                    {
                        Random random = new Random();
                        if (random.Next(0, 2) > 0) break;
                        await Task.Delay(5000);
                    }

                    if (!string.IsNullOrWhiteSpace(defHost) && !string.IsNullOrWhiteSpace(backupHost))
                    {
                        _debug.Remove(url);
                        url = url.Replace(defHost, backupHost);
                    }
                    continue;
                }
                catch (Exception ex)
                {
                    await Task.Delay(100);
                    if (ex.Message.Contains("429"))
                    {
                        _logger.Warn($"下载文件{url}时遇到429错误{ex.Message}" + " pid:" + pid);
                        await Task.Delay(new Random().Next(2000, 3000));
                        continue;
                    }
                    if (ex.Message.Contains("404"))
                    {
                        _logger.Warn($"下载文件{url}时遇到404错误{ex.Message}" + " pid:" + pid);
                        while (true)
                        {
                            Random random = new Random();
                            if (random.Next(0, 2) > 0) break;
                            await Task.Delay(5000);
                        }

                        if (!string.IsNullOrWhiteSpace(defHost) && !string.IsNullOrWhiteSpace(backupHost))
                        {
                            _debug.Remove(url);
                            url = url.Replace(defHost, backupHost);
                        }
                        
                        continue;
                    }
                    _logger.Warn($"下载文件{url}时遇到未知错误{ex.Message}" + " pid:" + pid);
                    continue;
                }

            }
            try { _debug.Remove(url); } catch { }

        }

        public DVersion ParseManifest(string versionId, string manifest)
        {
            JsonDocument document = JsonDocument.Parse(manifest);
            JsonElement versions = document.RootElement.GetProperty("versions");
            foreach (JsonElement version in versions.EnumerateArray())
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
            JsonDocument document = JsonDocument.Parse(manifest);
            List<DVersion> normal = new List<DVersion>();
            List<DVersion> alpha = new List<DVersion>();
            List<DVersion> beta = new List<DVersion>();
            JsonElement latest = document.RootElement.GetProperty("latest");
            // Parse versions
            JsonElement versions = document.RootElement.GetProperty("versions");
            foreach (JsonElement version in versions.EnumerateArray())
            {
                string id = version.GetProperty("id").GetString();
                string type = version.GetProperty("type").GetString();
                string url = version.GetProperty("url").GetString();
                string time = version.GetProperty("time").GetString();
                string releaseTime = version.GetProperty("releaseTime").GetString();
                DVersion verd;
                if (type.Equals("snapshot")) {
                    verd = new DVersion(id, 1, url, time, releaseTime);
                    normal.Add(verd);
                }
                else if (type.Equals("release"))
                {
                    verd = new DVersion(id, 0, url, time, releaseTime);
                    normal.Add(verd);
                }
                else if (type.Equals("old_alpha")) {
                    verd = new DVersion(id, 3, url, time, releaseTime);
                    alpha.Add(verd);
                }
                else if (type.Equals("old_beta")) {
                    verd = new DVersion(id, 2, url, time, releaseTime);
                    beta.Add(verd);
                }
                else
                {
                    _logger.Warn("Found new unknown version type: " + type);
                    continue;
                }
            }
            return (normal, alpha, beta);
        }

        async public Task<(List<string> forges, List<string> fabs, List<string> opts)> GetForgeFabricOptifineVersionList(string mcVersion)
        {
            //forge
            string json;
            using (var response = await s_client.GetAsync(_downloadSource.ForgeSupportedMc))
            {
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }
            JsonDocument document = JsonDocument.Parse(json);
            JsonElement element = document.RootElement;
            string[] versions = JsonSerializer.Deserialize<string[]>(json);
            List<string> forges = new List<string>();
            if(Array.Exists(versions, version => version == mcVersion))
            {
                using (var response = await s_client.GetAsync(_downloadSource.ForgeListViaMcVer.Replace("{mcversion}",mcVersion)))
                {
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync();
                }
                document = JsonDocument.Parse(json);
                foreach(JsonElement forgeVer in document.RootElement.EnumerateArray())
                {
                    forges.Add(forgeVer.GetProperty("version").GetString());
                }
                forges.Sort((v1, v2) => CompareVersions(v2,v1));
            }
            else
            {
                forges.Add("N");
            }
            //optifine
            using (var response = await s_client.GetAsync(_downloadSource.OptifineListViaMcVer.Replace("{mcversion}",mcVersion)))
            {
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }
            List<string> opts = new List<string>();
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(json.Trim()) || string.IsNullOrEmpty(json.Trim('[',']','{','}',' ')) || string.IsNullOrEmpty(json.Trim('[',']','{','}',' '))) { 
                opts.Add("N");
            }
            else
            {
                document = JsonDocument.Parse(json);
                foreach(JsonElement optVer in document.RootElement.EnumerateArray())
                {
                    string patch = optVer.GetProperty("patch").GetString();
                    opts.Insert(0, patch);
                }
            }
            //fabric
            List<string> fabs = new List<string>();
            using (var response = await s_client.GetAsync(_downloadSource.FabricManifest))
            {
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }
            document = JsonDocument.Parse(json);
            bool isEnable = false;
            foreach (JsonElement el in document.RootElement.GetProperty("game").EnumerateArray())
            {
                string version = el.GetProperty("version").GetString();
                if (version.Equals(mcVersion))
                {
                    isEnable = true;
                    break;
                }
            }
            if(isEnable)
            {
                foreach (JsonElement el in document.RootElement.GetProperty("loader").EnumerateArray())
                {
                    string version = el.GetProperty("version").GetString();
                    fabs.Add(version);
                }
            }
            else
            {
                fabs.Add("N");
            }
            return (forges, fabs, opts);
        }

        static int CompareVersions(string version1, string version2)
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