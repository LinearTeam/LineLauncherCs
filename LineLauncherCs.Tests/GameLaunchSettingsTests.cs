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

using LMC.Basic.Configs;
using LMCCore.Game.Launching.Configuration;
using LMCCore.Game.Model.Configuration;

namespace LineLauncherCs.Tests;

public class GameLaunchSettingsTests
{
    [Fact]
    public void Resolve_UsesVersionValuesAndFallsBackToGlobalValues()
    {
        var appConfig = new AppConfig
        {
            GameLaunch = new GameLaunchConfig
            {
                AutoAllocateMemory = true,
                MaxMemoryMb = 4096,
                JvmArguments = "-Dglobal=true",
                WrapperArguments = "cmd /c",
                GameArguments = "--global"
            }
        };
        var versionConfig = new GameVersionConfig
        {
            UseGlobalGameSettings = false,
            Java = new GameVersionJavaConfig
            {
                AutoSelectJava = false,
                SelectedJavaPath = "C:\\Java\\17",
                AutoAllocateMemory = false,
                MaxMemoryMb = 6144,
                JvmArguments = "-Dversion=true"
            },
            Game = new GameVersionGameConfig
            {
                GameArguments = "--version"
            }
        };

        var settings = GameLaunchSettingsResolver.Resolve(appConfig, versionConfig);

        Assert.False(settings.AutoSelectJava);
        Assert.Equal("C:\\Java\\17", settings.SelectedJavaPath);
        Assert.False(settings.AutoAllocateMemory);
        Assert.Equal(6144, settings.MaxMemoryMb);
        Assert.Equal("-Dversion=true", settings.JvmArguments);
        Assert.Equal("cmd /c", settings.WrapperArguments);
        Assert.Equal("--version", settings.GameArguments);
    }

    [Fact]
    public void Resolve_UsesGlobalValuesWhenVersionUsesGlobalSettings()
    {
        var appConfig = new AppConfig
        {
            SelectedJavaPath = "C:\\Java\\21",
            AutoSelectJava = false,
            GameLaunch = new GameLaunchConfig
            {
                MaxMemoryMb = 4096,
                JvmArguments = "-Dglobal=true"
            }
        };
        var versionConfig = new GameVersionConfig
        {
            Java = new GameVersionJavaConfig
            {
                AutoSelectJava = false,
                SelectedJavaPath = "C:\\Java\\8",
                MaxMemoryMb = 8192,
                JvmArguments = "-Dversion=true"
            },
            Game = new GameVersionGameConfig
            {
                GameArguments = "--version"
            }
        };

        var settings = GameLaunchSettingsResolver.Resolve(appConfig, versionConfig);

        Assert.False(settings.AutoSelectJava);
        Assert.Equal("C:\\Java\\21", settings.SelectedJavaPath);
        Assert.Equal(4096, settings.MaxMemoryMb);
        Assert.Equal("-Dglobal=true", settings.JvmArguments);
        Assert.Equal(string.Empty, settings.GameArguments);
    }

    [Fact]
    public void GetRecommendedAllocationMb_StaysWithinAvailableAndSafeBounds()
    {
        var memory = new SystemMemoryInfo(
            TotalBytes: 16L * 1024 * 1024 * 1024,
            AvailableBytes: 8L * 1024 * 1024 * 1024);

        Assert.Equal(4096, memory.GetRecommendedAllocationMb());
    }

    [Fact]
    public void CommandLineArgumentParser_PreservesQuotedArgumentValues()
    {
        var arguments = CommandLineArgumentParser.Parse("-Dpath=\"C:\\Program Files\\Java\" --username \"Player Name\"");

        Assert.Equal(
            ["-Dpath=C:\\Program Files\\Java", "--username", "Player Name"],
            arguments);
    }
}
