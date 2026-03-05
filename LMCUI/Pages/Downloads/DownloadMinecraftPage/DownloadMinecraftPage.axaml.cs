namespace LMCUI.Pages.Downloads.DownloadMinecraftPage;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using LMCCore.Utils;

public partial class DownloadMinecraftPage : PageBase {
    public DownloadMinecraftPage() : base("版本下载", "DownloadMinecraftPage") {
        InitializeComponent();
        Loaded += async (_, _) => {
            await Task.Delay(1000)
                .ContinueWith(async (_) => {
                    var url = "https://launchermeta.mojang.com/mc/game/version_manifest.json";
                    var response = await HttpUtils.CreateRequest(url)
                        .GetAsync();
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var ju = JsonUtils.Parse(json);
                    var versions = ju.GetArray<MinecraftVersionModel>("versions");
                    Dispatcher.UIThread.Invoke(() => UpdateVersions(versions));
                });
        };
    }
    private void UpdateVersions(List<MinecraftVersionModel>? versions) {
        var sb = (AutoCompleteBox)SearchBox;
        sb.ItemsSource = versions
            .Select(v => v.Id)
            .ToList();
        sb.FilterMode = AutoCompleteFilterMode.StartsWithOrdinal;

    }

    public class MinecraftVersionModel {
        [JsonPropertyName("id")] 
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("time")]
        public string Time { get; set; } = string.Empty;
        [JsonPropertyName("releaseTime")]
        public string ReleaseTime { get; set; } = string.Empty;

        public override string ToString() => Id;
    }
}