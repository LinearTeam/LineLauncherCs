namespace LMCCore.Java;

using System.Runtime.InteropServices;
using LMC;
using LMC.Basic;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using Microsoft.Win32;

public static class JavaManager {
    static readonly Logger s_logger = new("JavaManager");

    private static readonly object _configLock = new object();

    public async static Task AddJava(string javaPath, Action<TaskCallbackInfo>? callback = null, bool force = false) {
        callback ??= cbi => {};

        int prcId = new Random().Next(100, 999);
        
        javaPath = Path.GetFullPath(javaPath);
        s_logger.Info($"[添加 Java/{prcId}] : {javaPath}");

        
        int i = 1;  
        int total = 2;
        
        s_logger.Info($"[添加 Java/{prcId}] ({i} / {total}) : 校验Java");
        callback(new(i++, total,"校验 Java"));
        var isValid = await IsValidJavaRoot(javaPath);
        if (!isValid)
        {
            s_logger.Warn($"[添加 Java/{prcId}] 校验失败");
            throw new Exception("Invalid Java");
        }
        
        lock (_configLock) {
            if(!Current.Config.JavaPaths.Contains(javaPath)) 
                Current.Config.JavaPaths.Add(javaPath);
            ConfigManager.Save("app", Current.Config);
        }
        s_logger.Info($"[添加 Java/{prcId}] ({i} / {total}) : 完成添加");
        callback.Invoke(new(i++, total,"完成添加"));
    }

    public static void RemoveJava(string javaPath) {
        javaPath = Path.GetFullPath(javaPath);
        s_logger.Info($"禁用Java : {javaPath}");

        lock (_configLock) {
            if (Current.Config.JavaPaths.Contains(javaPath)) {
                Current.Config.JavaPaths.Remove(javaPath);
            }
            ConfigManager.Save("app", Current.Config);
        }
        
    }
    
    public async static Task<LocalJava> GetJavaInfo(string path) {
        path = Path.GetFullPath(path);
        s_logger.Info($"获取Java信息：{path}");
        var release = Path.Combine(path, "release");
        s_logger.Debug($"release 文件路径: {release}");
        var lines = await File.ReadAllLinesAsync(release);
        var ve = from l in lines
            where l.Replace("=", ":").StartsWith("JAVA_VERSION:", StringComparison.OrdinalIgnoreCase)
            select l;
        s_logger.Debug($"过滤的版本字符串: {string.Join(", ", ve)}");
        LocalJava java = new();
        java.Path = path;
        java.Version = Version.Parse(ve.First()
            .Replace("=",":")
            .Replace("JAVA_VERSION:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_",".")  //1.8.0_51
            .Replace("\"", ""));
        
        var impl = from l in lines
            where l.Replace("=", ":").StartsWith("IMPLEMENTOR:", StringComparison.OrdinalIgnoreCase)
            select l;
        
        s_logger.Debug($"过滤的发行商字符串: {string.Join(", ", impl)}");

        if (impl.Any())
        {
            java.Implementor = impl.First()
                .Replace("=",":")
                .Replace("IMPLEMENTOR:", "", StringComparison.OrdinalIgnoreCase)
                .Replace("\"", "");
        }
        
        java.IsJdk = File.Exists(Path.Combine(path, "bin", IsWindows() ? "javac.exe" : "javac"));
        
        s_logger.Debug(@$"最终Java：
路径：{path}
版本：{java.Version}
发行商：{java.Implementor}
是否是JDK：{java.IsJdk}");
        return java;
    }
    
    #pragma warning disable CA1416
    #region 搜索 

    public async static Task<List<string>> SearchJava(Action<TaskCallbackInfo> callback) {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        s_logger.Info("[Java 搜索] 正在搜索Java");
        int total = 5;
        int i = 1;
        
        s_logger.Info($"[Java 搜索] ({i}/{total}) 环境变量");
        callback.Invoke(new(i++, total, "Messages.JavaManager.SearchJava.Progress.Variables"));
        await FindViaEnvironmentVariables(paths);

        s_logger.Info($"[Java 搜索] ({i}/{total}) Registry");
        callback.Invoke(new(i++, total, "Messages.JavaManager.SearchJava.Progress.Registry"));
        await FindViaRegistry(paths);

        s_logger.Info($"[Java 搜索] ({i}/{total}) 默认路径");
        callback.Invoke(new(i++, total, "Messages.JavaManager.SearchJava.Progress.StandardPaths"));
        await FindViaStandardPaths(paths);

        s_logger.Info($"[Java 搜索] ({i}/{total}) MCRuntime");
        callback.Invoke(new(i++, total, "Messages.JavaManager.SearchJava.Progress.MinecraftRuntime"));
        await FindViaMinecraft(paths);

        s_logger.Info($"[Java 搜索] ({i}/{total}) LMCDir");
        callback.Invoke(new(i++, total, "Messages.JavaManager.SearchJava.Progress.CurrentDir"));
        await FindViaCurrentDirectory(paths);

        return [..paths];
    }

    // Variables
    async static Task FindViaEnvironmentVariables(HashSet<string> paths) {
        // JAVA_HOME
        string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome) && await IsValidJavaRoot(javaHome))
        {
            s_logger.Info($"[Java 搜索] JAVA_HOME: {javaHome}");
            paths.Add(Path.GetFullPath(javaHome));
        }

        // PATH
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string path in pathEnv.Split(Path.PathSeparator))
        {
            string javaBin = IsWindows()
                ? Path.Combine(path, "java.exe")
                : Path.Combine(path, "java");

            if (File.Exists(javaBin))
            {
                string fullPath = Path.GetFullPath(javaBin);
                string parentDir = Directory.GetParent(fullPath)?.Parent?.FullName;
                if (parentDir != null && await IsValidJavaRoot(parentDir))
                {
                    s_logger.Info($"[Java 搜索] PATH: {parentDir}");
                    paths.Add(Path.GetFullPath(parentDir));
                }
            }
        }
    }

    // regedit, generated by deepseek
    async static Task FindViaRegistry(HashSet<string> paths) {
        if (!IsWindows())
        {
            return;
        }

        string[] registryPaths =
        [
            @"SOFTWARE\JavaSoft\Java Runtime Environment", @"SOFTWARE\JavaSoft\Java Development Kit", @"SOFTWARE\Wow6432Node\JavaSoft\Java Runtime Environment", @"SOFTWARE\Wow6432Node\JavaSoft\Java Development Kit"
        ];

        using var baseKey = Registry.LocalMachine;
        foreach (string registryPath in registryPaths)
        {
            using var key = baseKey.OpenSubKey(registryPath);
            if (key == null)
            {
                continue;
            }

            foreach (string versionKey in key.GetSubKeyNames())
            {
                using var subKey = key.OpenSubKey(versionKey);
                var javaHome = subKey?.GetValue("JavaHome") as string;
                if (!string.IsNullOrEmpty(javaHome) && await IsValidJavaRoot(javaHome))
                {
                    s_logger.Info($"[Java 搜索] REG: {javaHome}");
                    paths.Add(Path.GetFullPath(javaHome));
                }
            }
        }
    }

    async static Task FindViaStandardPaths(HashSet<string> paths) {
        if (IsWindows())
        {
            await CheckCommonPaths(paths,
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdks")
            ]);
        }
        else if (IsMacOS())
        {
            await CheckCommonPaths(paths,
            [
                "/Library/Java/JavaVirtualMachines",
                "/usr/libexec/java_home",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdks")
            ]);
        }
        else if (IsLinux())
        {
            await CheckCommonPaths(paths,
            [
                "/usr/lib/jvm",
                "/usr/java",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jdks")
            ]);
        }
    }

    async static Task CheckCommonPaths(HashSet<string> paths, IEnumerable<string> directories) {
        foreach (string dir in directories)
        {
            if (Directory.Exists(dir))
            {
                await ScanDirectoryRecursively(dir, paths);
            }
        }
    }

    async static Task FindViaMinecraft(HashSet<string> paths) {
        string mcRuntimePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ".minecraft",
        "runtime"
        );

        if (Directory.Exists(mcRuntimePath))
        {
            await ScanDirectoryRecursively(mcRuntimePath, paths);
        }
    }

    async static Task FindViaCurrentDirectory(HashSet<string> paths) {
        await ScanDirectoryRecursively(Directory.GetCurrentDirectory(), paths);
    }

    async static Task ScanDirectoryRecursively(string directory, HashSet<string> paths, int depth = 0) {
        if(depth >= 4) return;
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            if (await IsValidJavaRoot(directory))
            {
                s_logger.Info($"[Java 搜索] Dir: {directory}");
                paths.Add(Path.GetFullPath(directory));
                return;
            }

            foreach (string subdir in Directory.GetDirectories(directory))
            {
                await ScanDirectoryRecursively(subdir, paths, depth++);
            }
        }
        catch(Exception ex) { s_logger.Error(ex, "Scan directory for Java"); }
    }

    async static Task<bool> IsValidJavaRoot(string path) {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return false;
        }

        string javaExe = Path.Combine(path, "bin", IsWindows() ? "java.exe" : "java");
        try
        {
            await GetJavaInfo(path);
        }
        catch (Exception ex)
        {
            s_logger.Debug($"无效 Java : {path} ({ex.Message})");
            return false;
        }
        return File.Exists(javaExe);
    }

    static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    static bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    #endregion
    #pragma warning restore CA1416

}
