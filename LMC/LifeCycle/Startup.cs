namespace LMC.LifeCycle;

using System.Runtime.InteropServices;
using System.Text.Json;
using Basic.Configs;
using Basic.Logging;

public class Startup {

    static Logger s_logger;
    
    public async static Task Initialize(Action<(int progress, int total, string msg)> callback) {
        int total = 2;
        int i = 1;
        
        callback?.Invoke((i++, total, "创建必要文件"));
        CreateDirectory();

        await Task.Delay(3000);
        
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
        
        callback?.Invoke((i++, total, "加载配置文件"));
        LoadConfiguration();
    }

    private static void CreateDirectory() {
        Directory.CreateDirectory(Current.LMCPath);
    }

    
    private static void LoadConfiguration() {
        s_logger.Info("正在加载配置文件");
        var cfg = ConfigManager.Load<AppConfig>("app");
        s_logger.Info($"最终配置文件：\n{JsonSerializer.Serialize(cfg, 
            new JsonSerializerOptions {WriteIndented = true})}");
        Current.Config = cfg;
    }
    
}
