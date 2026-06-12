// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
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

    [Fact]
    public void AppConfig_DefaultVersionConfigSourceForNewInstalls_DefaultsToVersionJson()
    {
        var config = new AppConfig();

        Assert.Equal(NewVersionConfigSource.VersionJson, config.DefaultVersionConfigSourceForNewInstalls);
    }

    [Fact]
    public void Load_AppConfig_PreservesDefaultVersionConfigSourceForNewInstalls()
    {
        using var scope = new TestFileSystemScope();
        ConfigManager.ConfigDirectoryOverride = scope.RootPath;

        try
        {
            var config = new AppConfig
            {
                DefaultVersionConfigSourceForNewInstalls = NewVersionConfigSource.VersionFolder
            };
            ConfigManager.Save("app", config);

            var loaded = ConfigManager.Load<AppConfig>("app");

            Assert.Equal(
                NewVersionConfigSource.VersionFolder,
                loaded.DefaultVersionConfigSourceForNewInstalls);
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
