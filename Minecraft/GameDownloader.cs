using LMC.Account.OAuth;
using LMC.Basic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

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

        public (List<DVersion> normal, List<DVersion> alpha, List<DVersion> beta) ParseManifest(String manifest)
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
        async public Task DownloadVersionJsonVanilla(DVersion version, String name)
        {
            string json;
            using (var response = await s_client.GetAsync(version.Url))
            {
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }
            json = json.Replace($"\"id\": \"{version.Id}\"", $"\"id\": \"{name}\"");
            Directory.CreateDirectory(GamePath + $"/versions/{name}");
            File.Create(GamePath + $"/versions/{name}/{name}.json").Close();
            json.Replace("\r\n", "\n");
            File.WriteAllText(GamePath + $"/versions/{name}/{name}.json", json);
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