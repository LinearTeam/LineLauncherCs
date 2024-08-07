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
    public class GameDownload
    {
        public static String gamePath = @"./.minecraft";
        private static HttpClient client = new HttpClient();
        private DownloadSource downloadSource = new DownloadSource();
        private Logger logger = new Logger("GD");

        public void ChangeSource(DownloadSource newSource)
        {
            if (newSource == downloadSource || newSource == null) return;
            this.downloadSource = newSource;
        }

        public void BMCLAPI()
        {
            downloadSource.BMCLAPI();
        }

        async public Task<String> GetVersionManifest()
        {
            return await client.GetStringAsync(downloadSource.VersionManifest);
        }

        public (List<VersionDownload> normal, List<VersionDownload> alpha, List<VersionDownload> beta) ManifestParse(String manifest) 
        { 
            JsonDocument document = JsonDocument.Parse(manifest);
            List<VersionDownload> normal = new List<VersionDownload>();
            List<VersionDownload> alpha = new List<VersionDownload>();
            List<VersionDownload> beta = new List<VersionDownload>();
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
                VersionDownload verd;
                if(type.Equals("snapshot")) {
                    verd = new VersionDownload(id, 1, url, time, releaseTime);
                    normal.Add(verd);
                }
                else if (type.Equals("release"))
                {
                    verd = new VersionDownload(id, 0, url, time, releaseTime);
                    normal.Add(verd);
                }
                else if(type.Equals("old_alpha")) {
                    verd = new VersionDownload(id, 3, url, time, releaseTime);
                    alpha.Add(verd);
                }
                else if(type.Equals("old_beta")) {
                    verd = new VersionDownload(id, 2, url, time, releaseTime);
                    beta.Add(verd);
                }
                else
                {
                    logger.warn("Found new unknown version type: " + type);
                    continue;
                }
            }
            return (normal,alpha,beta);
        }
        async public Task DownloadVersionJsonVanilla(VersionDownload version, String name)
        {
            string json;
            using (var response = await client.GetAsync(version.url))
            {
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }
            json = json.Replace($"\"id\": \"{version.id}\"", $"\"id\": \"{name}\"");
            Directory.CreateDirectory(gamePath + $"/versions/{name}");
            File.Create(gamePath + $"/versions/{name}/{name}.json").Close();
            json.Replace("\r\n", "\n");
            File.WriteAllText(gamePath + $"/versions/{name}/{name}.json", json);
        }


    }
}