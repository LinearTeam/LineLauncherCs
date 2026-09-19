namespace LMC.Tests;

internal sealed class TestFileSystemScope : IDisposable
{
    public TestFileSystemScope()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "LMC.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string CreateDirectory(string relativePath)
    {
        var path = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    public string GetPath(string relativePath)
    {
        return Path.Combine(RootPath, relativePath);
    }

    public void Dispose()
    {
        if (!Directory.Exists(RootPath))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(RootPath, true);
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }
        }
    }
}
