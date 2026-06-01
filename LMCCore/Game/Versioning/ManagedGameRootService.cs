using LMC.Basic.Configs;
using LMCCore.Game.Model;

namespace LMCCore.Game.Versioning;

internal sealed class ManagedGameRootService(AppConfig config, Action saveConfig)
{
    private readonly AppConfig _config = config;
    private readonly Action _saveConfig = saveConfig;

    public IReadOnlyList<ManagedGameRoot> GetManagedRoots()
    {
        return _config.ManagedGameRootPaths
            .Select(path => new ManagedGameRoot
            {
                RootPath = VersionPathUtils.NormalizePath(path)
            })
            .ToList()
            .AsReadOnly();
    }

    public void AddManagedRoot(string path)
    {
        var normalizedPath = EnsureExistingDirectory(path);
        if (_config.ManagedGameRootPaths.Any(existing =>
                string.Equals(VersionPathUtils.NormalizePath(existing), normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _config.ManagedGameRootPaths.Add(normalizedPath);
        if (string.IsNullOrWhiteSpace(_config.SelectedGameRootPath))
        {
            _config.SelectedGameRootPath = normalizedPath;
        }

        _saveConfig();
    }

    public bool RemoveManagedRoot(string path)
    {
        var normalizedPath = VersionPathUtils.NormalizePath(path);
        var removed = _config.ManagedGameRootPaths.RemoveAll(existing =>
            string.Equals(VersionPathUtils.NormalizePath(existing), normalizedPath, StringComparison.OrdinalIgnoreCase)) > 0;

        if (!removed)
        {
            return false;
        }

        if (string.Equals(VersionPathUtils.NormalizePathOrEmpty(_config.SelectedGameRootPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            _config.SelectedGameRootPath = _config.ManagedGameRootPaths.FirstOrDefault() ?? string.Empty;
        }

        _saveConfig();
        return true;
    }

    public void SetSelectedRoot(string path)
    {
        var normalizedPath = EnsureExistingDirectory(path);
        if (!_config.ManagedGameRootPaths.Any(existing =>
                string.Equals(VersionPathUtils.NormalizePath(existing), normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            _config.ManagedGameRootPaths.Add(normalizedPath);
        }

        _config.SelectedGameRootPath = normalizedPath;
        _saveConfig();
    }

    public ManagedGameRoot? GetSelectedRoot()
    {
        if (string.IsNullOrWhiteSpace(_config.SelectedGameRootPath))
        {
            return null;
        }

        return new ManagedGameRoot
        {
            RootPath = VersionPathUtils.NormalizePath(_config.SelectedGameRootPath)
        };
    }

    internal static string EnsureExistingDirectory(string path)
    {
        var normalizedPath = VersionPathUtils.NormalizePath(path);
        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"游戏目录不存在: {normalizedPath}");
        }

        return normalizedPath;
    }
}
