using System.Collections.ObjectModel;
using System.Diagnostics;

namespace LMC.LifeCycle;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Basic.Configs;
using Basic.Logging;

public class Startup {

    static Logger s_logger = null!;
    
    //DO NOT TRANSLATE
    public static Task Initialize() {

        CreateDirectory();

        s_logger = new Logger("Startup");
        
        s_logger.Info("正在初始化LMC");
        s_logger.Info("=============LMC 运行信息=============");
        s_logger.Info($"LMC 文件位置： {Environment.ProcessPath}");
        s_logger.Info($"LMC 数据目录： {Current.LMCPath}");
        s_logger.Info($"LMC 版本号： {Current.Version} - {Current.BuildNumber} - {Current.VersionType}");
        s_logger.Info($"用户系统信息： {RuntimeInformation.OSDescription} ");
        s_logger.Info($"用户系统架构： {RuntimeInformation.OSArchitecture.ToString()}");
        s_logger.Info($"RuntimeID： {RuntimeInformation.RuntimeIdentifier}");
        s_logger.Info($".NET 运行环境： {RuntimeInformation.FrameworkDescription}");
        s_logger.Info("=============LMC 运行信息=============");
        HandleArguments();
        PreInit();
        LoadConfiguration();
        return Task.CompletedTask;
    }

    private static void PreInit()
    {
        if (Current.Arguments.TryGetValue("restart", out string? pidStr))
        {
            if (int.TryParse(pidStr, out var pid))
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    s_logger.Info($"正在终止进程 {pid}...");
                    process.Kill();
                    s_logger.Info($"进程 {pid} 已终止，继续启动LMC");
                }
                catch (Exception ex)
                {
                    s_logger.Error(ex, "终止上一个进程");
                }
            }else
            {
                s_logger.Warn($"无法解析上一进程ID: {pidStr}");
            }
        }
    }

    private static void HandleArguments()
    { 
        var dict = new Dictionary<string, string>();
        var args = Environment.GetCommandLineArgs();
        foreach (var arg in args) {
            if (arg.StartsWith("--")) {
                var split = arg.Substring(2).Split('=', 2);
                var key = split[0];
                var value = split.Length > 1 ? split[1] : "true";
                dict[key] = value;
            }
        }
        Current.Arguments = new ReadOnlyDictionary<string, string>(dict);
    }
    
    private static void CreateDirectory() {
        Directory.CreateDirectory(Current.LMCPath);
        File.Delete(Path.Combine(Current.LMCPath, "logs", "latest.log"));
    }

    
    private static void LoadConfiguration() {
        s_logger.Info("正在加载配置文件");
        var cfg = ConfigManager.Load<AppConfig>("app");
        s_logger.Info($"最终配置文件：\n{JsonSerializer.Serialize(cfg, 
            new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver()
            })}");
        Current.Config = cfg;
    }
}
