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

using LMCCore.Game.Launching.Steps;
using LMCCore.Game.Launching.Steps.Account;
using LMCCore.Game.Launching.Steps.Arguments;
using LMCCore.Game.Launching.Steps.PreLaunch;
using LMCCore.Game.Launching.Steps.Resources;
using LMCCore.Game.Launching.Steps.Startup;

namespace LMCCore.Game.Launching.Providers;

internal static class GameLaunchStepProvider
{
    public static IReadOnlyList<IGameLaunchStepHandler> CreateDefault()
    {
        return
        [
            new PreLaunchCheckStepHandler(),
            new DownloadMissingFilesStepHandler(),
            new ProcessAccountStepHandler(),
            new ProcessLaunchArgumentsStepHandler(),
            new ExtractLocalLibrariesStepHandler(),
            new StartGameStepHandler(),
            new WaitForGameWindowStepHandler()
        ];
    }
}
