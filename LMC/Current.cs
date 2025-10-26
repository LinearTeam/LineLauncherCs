namespace LMC;

using Basic.Configs;

public static class Current
{
    public const string Version = "3.0.0";

    public const string BuildNumber = "0009";

    public const string VersionType = "alpha";
    
    public static readonly string LMCPath =  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LMC");
    public static AppConfig? Config { get; set; }

    public static void SaveConfig() {
        ConfigManager.Save("app", Config);
    }
}