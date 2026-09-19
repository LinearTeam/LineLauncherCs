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
using LMCCore.Game.Model.Configuration;

namespace LMCCore.Game.Launching.Configuration;

public static class GameLaunchSettingsResolver
{
    public static GameLaunchSettings Resolve(AppConfig appConfig, GameVersionConfig? versionConfig)
    {
        ArgumentNullException.ThrowIfNull(appConfig);

        var globalConfig = appConfig.GameLaunch ?? new GameLaunchConfig();
        var useGlobalGameSettings = versionConfig?.UseGlobalGameSettings != false;
        var javaConfig = useGlobalGameSettings ? null : versionConfig?.Java;
        var gameConfig = useGlobalGameSettings ? null : versionConfig?.Game;

        return new GameLaunchSettings(
            javaConfig?.AutoSelectJava ?? appConfig.AutoSelectJava,
            javaConfig?.SelectedJavaPath ?? appConfig.SelectedJavaPath,
            javaConfig?.AutoAllocateMemory ?? globalConfig.AutoAllocateMemory,
            Math.Max(512, javaConfig?.MaxMemoryMb ?? globalConfig.MaxMemoryMb),
            javaConfig?.JvmArguments ?? globalConfig.JvmArguments,
            javaConfig?.WrapperArguments ?? globalConfig.WrapperArguments,
            gameConfig?.GameArguments ?? globalConfig.GameArguments);
    }
}
