namespace LMC.Basic.Configs;

using Logging;

[ConfigVersion(1)]
public class AppConfig {
    public LogLevel LogLevel { get; set; } = LogLevel.Info;
    
    public List<string> JavaPaths { get; set; } = new();
    
    public string SelectedLanguage { get; set; } = "zh-CN";
}

