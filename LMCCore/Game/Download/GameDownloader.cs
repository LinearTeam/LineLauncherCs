namespace LMCCore.Game.Download;

using Java;
using LMC.Basic.Logging;

public class GameDownloader(GameDownloaderOptions options, CancellationToken? ct) : IAsyncDisposable {
    private static readonly Logger s_logger= new Logger("GameDownloader");
    public async Task DownloadGameAsync() {
        if (options.JsonPath != null && File.Exists(options.JsonPath))
        {
            
        }
    }
    public async ValueTask DisposeAsync(){
        if (ct is { IsCancellationRequested: true })
        {
            int retries = 0;
            retry:
            try
            {
                Directory.Delete(Path.Combine(options.GameDirectory, "versions", options.VersionName), true);
            }
            catch (Exception ex)
            {
                s_logger.Error(ex, "Cancelling GameDownloader");
                if(retries < 5) retries++;
                else throw new Exception($"Cancelling game download failed: {ex.Message}", ex);
                await Task.Delay(500);
                goto retry;
            }
        }
    }
}

public record GameDownloaderOptions(string VanillaId, string VersionName, string GameDirectory, LocalJava? UsableJava, string? JsonUrl, string? JsonPath) {
    
}
