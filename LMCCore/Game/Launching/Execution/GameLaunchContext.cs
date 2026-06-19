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

using System.Diagnostics;
using LMC.Basic.Configs;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Launching.Steps.Arguments;
using LMCCore.Game.Model;

namespace LMCCore.Game.Launching.Execution;

public sealed class GameLaunchContext
{
    public required LocalGameVersionEntry Version { get; init; }

    public required AppConfig Config { get; init; }

    public required DownloadSourceManager DownloadSourceManager { get; init; }

    public required VanillaGameDownloader VanillaDownloader { get; init; }

    public bool ShouldValidateAndCompleteMissingFiles { get; init; } = true;

    public Process? Process { get; set; }

    public bool WindowDetected { get; set; }
    
    public CommandBuilder? CommandBuilder { get; set; }
    
    public required Account.Model.Account Account { get; set; }
}
