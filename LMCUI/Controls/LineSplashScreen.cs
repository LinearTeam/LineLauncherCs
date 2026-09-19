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

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using FluentAvalonia.UI.Windowing;
using LMCCore.Game.Versioning;

namespace LMCUI.Controls;

public class LineSplashScreen : IFAApplicationSplashScreen
{
    public event EventHandler? Completed;

    public async Task RunTasks(CancellationToken cancellationToken)
    {
        new VersionManager().StartWarmSelectedRootVersions();

        await ((LineSplashScreenContent)SplashScreenContent).InitializeAsync(cancellationToken);
        Completed?.Invoke(this, EventArgs.Empty);
    }

    public string AppName { get; init; }
    public IImage AppIcon { get; init; }
    public object SplashScreenContent { get; } = new LineSplashScreenContent();
    public int MinimumShowTime { get; init; } = 1500;
}
