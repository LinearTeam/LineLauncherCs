namespace LMC.Basic.Configs;

using Logging;

[ConfigVersion(1)]
public class AppConfig {
    public LogLevel LogLevel { get; set; } = LogLevel.Info;
}
