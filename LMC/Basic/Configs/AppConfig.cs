namespace LMC.Basic.Configs;

using System.Collections.Concurrent;
using Logging;

[ConfigVersion(1)]
public class AppConfig {
    public LogLevel LogLevel { get; set; } = LogLevel.Info;
    
    public List<string> JavaPaths { get; set; } = new();

    public string SelectedJavaPath { get; set; } = string.Empty;

    public bool AutoSelectJava { get; set; } = true;
    public string SelectedLanguage { get; set; } = "zh-CN";
    
    
}

