using LMC.Basic.Logging;

namespace LMCCore.Java;

internal sealed class JavaDirectoryScanner(
    Func<string, Task<bool>> validateJavaRootAsync,
    Logger logger,
    int maxDepth = 4)
{
    private readonly Func<string, Task<bool>> _validateJavaRootAsync = validateJavaRootAsync;
    private readonly Logger _logger = logger;
    private readonly int _maxDepth = maxDepth;

    public async Task ScanDirectoryRecursivelyAsync(string directory, ISet<string> paths, int depth = 0)
    {
        if (depth >= _maxDepth || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            if (await _validateJavaRootAsync(directory))
            {
                _logger.Info($"[Java 搜索] Dir: {directory}");
                paths.Add(JavaPathNormalizer.NormalizeRootPath(directory));
                return;
            }

            foreach (var subDirectory in Directory.GetDirectories(directory))
            {
                await ScanDirectoryRecursivelyAsync(subDirectory, paths, depth + 1);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Scan directory for Java");
        }
    }
}
