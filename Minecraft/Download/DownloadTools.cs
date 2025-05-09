#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Documents;
using LMC.Basic;
using LMC.Minecraft.Download.Exceptions;
using LMC.Minecraft.Download.Model;
using LMC.Minecraft.Profile.Model;
using LMC.Utils;

namespace LMC.Minecraft.Download
{
    public class DownloadTools
    {
        
        private static Logger s_logger = new Logger("DT");
        
        /// <summary>
        /// 返回解析后的<see cref="LibraryFile"/>列表
        /// </summary>
        /// <param name="libraryJson">LibrariesJson数组</param>
        /// <returns></returns>
        public static List<LibraryFile> ParseLibraryFiles(string libraryJson)
        {
            List<LibraryFile> libraryFiles = new List<LibraryFile>();
            var larr = JsonNode.Parse(libraryJson).AsArray();
            foreach (var lib in larr)
            {
                var libStr = lib.ToString();
                if(!string.IsNullOrEmpty(JsonUtils.GetValueFromJson(libStr, "downloads")))
                {
                    VanillaLibraryJson? vlj = JsonSerializer.Deserialize<VanillaLibraryJson>(libStr);
                    if (vlj == null)
                    {
                        s_logger.Error("Null VanillaLibraryJson :\n" + libStr);
                        throw new InvalidLibraryJsonException(null, "解析出的VanillaLibraryJson为null，详情请见日志。");
                    }

                    libraryFiles.AddRange(VanillaLibraryJsonToLibraryFile(vlj));
                }
                else
                {
                    var name = JsonUtils.GetValueFromJson(libStr, "name");
                    var url = JsonUtils.GetValueFromJson(libStr, "url");
                    if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(name))
                    {
                        s_logger.Error("Unknown type of library : \n" + libStr);
                        throw new InvalidLibraryJsonException(null, "解析出的fabric library中没有name/url，详情请见日志。");
                    }

                    var f = new LibraryFile();
                    f.Name = JsonUtils.GetValueFromJson(libStr, "name");
                    url = url + "/" + GetLibraryFile(name);
                    f.Url = url;
                    f.Path = GetLibraryFileName(name);
                    if(!string.IsNullOrEmpty(JsonUtils.GetValueFromJson(libStr, "sha1"))) f.Sha1 = JsonUtils.GetValueFromJson(libStr, "sha1");
                    if(!string.IsNullOrEmpty(JsonUtils.GetValueFromJson(libStr, "size"))) f.Size = long.Parse(JsonUtils.GetValueFromJson(libStr, "size"));
                    libraryFiles.Add(f);
                }
            }
            return libraryFiles;
        }

        
        /// <summary>
        /// 解析资源索引文件中所有<c>objects</c>
        /// </summary>
        /// <param name="indexJson">资源索引文件</param>
        /// <param name="downloadSource">下载源</param>
        /// <param name="rootDirectory">.minecraft目录</param>
        /// <returns></returns>
        public static List<NetFile> ParseAssetObjects(string indexJson, DownloadSource downloadSource, string rootDirectory)
        {
            List<NetFile> netFiles = new List<NetFile>();
            
            string objects = JsonUtils.GetValueFromJson(indexJson, "objects");
            using (var doc = JsonDocument.Parse(objects))
            {
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in root.EnumerateObject())
                    {
                        string hash = JsonUtils.GetValueFromJson(property.Value.ToString(), "hash");
                        string url = $"{downloadSource.ResourcesDownload}/{hash.Substring(0, 2)}/{hash}";
                        string path = $"{rootDirectory}/assets/objects/{hash.Substring(0, 2)}/{hash}";
                        netFiles.Add(new NetFile(){
                            Url = url,
                            Hash = hash,
                            Path = path
                        });
                    }
                }
            }
            return netFiles;
        }
        
        /// <summary>
        /// 将<see cref="LibraryFile"/>列表转换为<see cref="NetFile"/>列表以便下载。
        /// </summary>
        /// <param name="libraryFiles">原列表</param>
        /// <param name="rootDirectory">.minecraft目录</param>
        /// <returns></returns>
        public static List<NetFile> LibraryFilesToNetFiles(List<LibraryFile> libraryFiles, string rootDirectory)
        {
            List<NetFile> netFiles = new List<NetFile>();
            libraryFiles.ForEach(f =>
            {
                var netFile = new NetFile();
                netFile.Path = rootDirectory + "/libraries/" + f.Path;
                netFile.Url = f.Url;
                netFile.Hash = f.Sha1;
                netFiles.Add(netFile);
            });
            return netFiles;
        }

        /// <summary>
        /// 将<see cref="VanillaLibraryJson"/>中的项提取为<see cref="LibraryFile"/>
        /// </summary>
        /// <param name="libraryJson">传入的LibraryJson</param>
        /// <returns></returns>
        /// <exception cref="InvalidLibraryJsonException">CLassifiers无法找到对应的key</exception>
        public static List<LibraryFile> VanillaLibraryJsonToLibraryFile(VanillaLibraryJson libraryJson)
        {
            List<LibraryFile> libraryFiles = new List<LibraryFile>();
            if (libraryJson.Name.Contains("linux") || libraryJson.Name.Contains("macos") || libraryJson.Name.Contains("arm"))
            {
                return libraryFiles;                
            }
            bool windows = true;
            
            
            // foreach (var r in libraryJson.Rules)
            // {
            //     if(r == null) continue;
            //     if((r.Os == null || r.Os.System == null || r.Os.Arch == null || r.Os.Version == null) && r.Action == "allow") windows = true;
            //     if(r.Os == null && r.Action == "disallow") windows = false;
            //     if (r.Os.System == "windows" && r.Action == "allow") {  windows = true; break; }
            //     if(r.Os.System == "windows" && r.Action == "disallow"){ windows = false; break; }
            //     if(r.Os.System != null && r.Os.System != "windows" && r.Action == "allow") windows = false;
            //     if(r.Os.System != "windows" && r.Action == "disallow") windows = true;
            // }

            
            if(!windows) return libraryFiles;
            if (libraryJson.Downloads.Artifact != null)
            {
                var lf = new LibraryFile();
                lf.Rules = libraryJson.Rules.ToArray();
                lf.Name = libraryJson.Name;
                lf.Path = libraryJson.Downloads.Artifact.Path;
                lf.Size = libraryJson.Downloads.Artifact.Size;
                lf.Url = libraryJson.Downloads.Artifact.Url;
                lf.Sha1 = libraryJson.Downloads.Artifact.Sha1;
                lf.IsNativeLib = libraryJson.Name.Contains("natives");
                libraryFiles.Add(lf);
            }

            if (libraryJson.Natives != null && libraryJson.Natives.ContainsKey("windows") && libraryJson.Downloads.Classifiers != null)
            {
                var key = libraryJson.Natives["windows"].Replace("${arch}", "64");
                var lf = new LibraryFile();
                var df = libraryJson.Downloads.Classifiers[key];
                if(df == null) throw new InvalidLibraryJsonException(libraryJson, "无法在Classifiers中找到对应的key : " + key);
                lf.Rules = libraryJson.Rules.ToArray();
                lf.Name = libraryJson.Name;
                lf.Path = df.Path;
                lf.Size = df.Size;
                lf.Url = df.Url;
                lf.Sha1 = df.Sha1;
                lf.IsNativeLib = true;
                libraryFiles.Add(lf);
            }
            return libraryFiles;
        }
        
        // Region T From https://github.com/TT702/Forge-InstallProcessor.NET

        #region T
        public static string GetLibraryFile(string name) {
            if(name.StartsWith("[") && name.EndsWith("]")) {
                name = name.Replace("[", "").Replace("]", "");
            }

            if (name.StartsWith("{") && name.EndsWith("}"))
            {
                name = name.Replace("{", "").Replace("}", "");
            }
            
            return $"{GetLibraryFileName(name)}";
        }

        public static string GetLibraryFileName(string name) {
            var extinction = ".jar";
            if (name.Contains("@")) {
                extinction =  $".{name.Substring(name.LastIndexOf('@') + 1)}";
                name = name.Substring(0, name.LastIndexOf('@'));
            }

            string[] targets = name.Split(':');
            if (targets.Length < 3) return null;
            else {
                var pathBase = string.Join("\\", targets[0].Replace('.', '\\'), targets[1], targets[2], targets[1]) + '-' + targets[2];
                for(var i = 3; i < targets.Length; i++) {
                    pathBase = $"{pathBase}-{targets[i]}";
                }

                pathBase = $"{pathBase}{extinction}";
                return pathBase;
            }
        }
        public static string GetMainClassFromJar(string MainJarPath) {
            using (var zip = ZipFile.Open(MainJarPath, ZipArchiveMode.Read)) {
                var mainfest = zip.GetEntry("META-INF/MANIFEST.MF");
                var stream = new StreamReader(mainfest.Open());
                var currentLine = String.Empty;

                while ((currentLine = stream.ReadLine()) != null) {
                    if (currentLine.Contains("Main-Class:")) {
                        stream.Close();
                        return $"{currentLine.Replace("Main-Class:", "").Trim()} ";
                    }
                }
                stream.Close();
                return "";
            }
        }
        #endregion
    }
}