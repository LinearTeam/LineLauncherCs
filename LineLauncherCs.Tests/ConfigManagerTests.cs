using System.Text.Json.Nodes;
using LMC.Basic.Configs;

namespace LineLauncherCs.Tests;

public class ConfigManagerTests
{
    [Fact]
    public void Load_MigratesAliasProcessorAndRemovedFieldsAcrossVersions()
    {
        using var scope = new TestFileSystemScope();
        ConfigManager.ConfigDirectoryOverride = scope.RootPath;
        var filePath = scope.GetPath("migration.config.json");

        File.WriteAllText(filePath, """
        {
          "$version": 1,
          "legacyName": "alpha",
          "OldFlag": "remove-me"
        }
        """);

        try
        {
            var config = ConfigManager.Load<MigrationTestConfig>("migration");

            Assert.Equal("alpha-migrated", config.CurrentName);
            Assert.Null(config.OldFlag);

            var persistedJson = JsonNode.Parse(File.ReadAllText(filePath))!.AsObject();
            Assert.Equal(3, persistedJson["$version"]!.GetValue<int>());
            Assert.Equal("alpha-migrated", persistedJson["CurrentName"]!.GetValue<string>());
            Assert.False(persistedJson.ContainsKey("OldFlag"));
        }
        finally
        {
            ConfigManager.ConfigDirectoryOverride = null;
        }
    }

    [Fact]
    public void Load_InvalidJsonReturnsDefaultConfig()
    {
        using var scope = new TestFileSystemScope();
        ConfigManager.ConfigDirectoryOverride = scope.RootPath;
        File.WriteAllText(scope.GetPath("invalid.config.json"), "{not-json");

        try
        {
            var config = ConfigManager.Load<MigrationTestConfig>("invalid");

            Assert.Equal("default", config.CurrentName);
        }
        finally
        {
            ConfigManager.ConfigDirectoryOverride = null;
        }
    }

    [ConfigVersion(3)]
    public class MigrationTestConfig
    {
        [ConfigAlias("legacyName", untilVersion: 2)]
        public string CurrentName { get; set; } = "default";

        [ConfigRemoved(3)]
        public string? OldFlag { get; set; }

        [ConfigUpgradeProcessor(2, 3)]
        private static void Upgrade(JsonObject config)
        {
            if (config["CurrentName"] is JsonValue value &&
                value.TryGetValue<string>(out var text))
            {
                config["CurrentName"] = $"{text}-migrated";
            }
        }
    }
}
