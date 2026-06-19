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
using LMC.Basic.Logging;
using LMCCore.Game.Launching.Execution;

namespace LMCCore.Game.Launching.Steps.Startup;

public sealed class WaitForGameWindowStepHandler : IGameLaunchStepHandler
{
    private readonly static Logger s_logger = new("Launch");

    public GameLaunchProgressStep Step => GameLaunchProgressStep.WaitForGameWindow;

    public async Task ExecuteAsync(GameLaunchContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Process);

        var process = context.Process;
        var status = GameStatus.Waiting;
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.WindowDetected = false;
        process.EnableRaisingEvents = true;

        DataReceivedEventHandler outputHandler = (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            s_logger.Debug($"[{process.Id}] {e.Data}");

            if (status == GameStatus.TexturesLoaded)
            {
                return;
            }

            if (status == GameStatus.Waiting)
            {
                status = GameStatus.LogOutput;
                s_logger.Info($"[{process.Id}] [1] 已有日志输出");
                return;
            }

            if (e.Data.Contains("Setting user", StringComparison.OrdinalIgnoreCase) &&
                status == GameStatus.LogOutput)
            {
                status = GameStatus.SetUser;
                s_logger.Info($"[{process.Id}] [2] 游戏账户已设置");
                return;
            }

            if (e.Data.Contains("lwjgl version", StringComparison.OrdinalIgnoreCase) &&
                status == GameStatus.SetUser)
            {
                status = GameStatus.LwjglLoaded;
                s_logger.Info($"[{process.Id}] [3] LWJGL 已加载");
                return;
            }

            if ((e.Data.Contains("OpenAL initialized", StringComparison.OrdinalIgnoreCase) ||
                 e.Data.Contains("Starting up SoundSystem", StringComparison.OrdinalIgnoreCase)) &&
                status == GameStatus.LwjglLoaded)
            {
                status = GameStatus.OpenALLoaded;
                s_logger.Info($"[{process.Id}] [4] 声音引擎已加载");
                return;
            }

            if ((e.Data.Contains("Found animation info", StringComparison.OrdinalIgnoreCase) ||
                 (e.Data.Contains("Created", StringComparison.OrdinalIgnoreCase) &&
                  e.Data.Contains("textures", StringComparison.OrdinalIgnoreCase) &&
                  e.Data.Contains("-atlas", StringComparison.OrdinalIgnoreCase))) &&
                status == GameStatus.OpenALLoaded)
            {
                status = GameStatus.TexturesLoaded;
                context.WindowDetected = true;
                s_logger.Info($"[{process.Id}] [5] 材质资源已加载，游戏已启动");
                completionSource.TrySetResult(true);
            }
        };

        EventHandler exitedHandler = (_, _) =>
        {
            if (!completionSource.TrySetResult(false))
                return;
            context.WindowDetected = false;
            s_logger.Info($"[{process.Id}] 游戏进程已退出，退出码 {process.ExitCode} ，未检测到窗口就绪状态");
            if (process.ExitCode != 0)
            {
                s_logger.Error($"游戏进程 {process.Id} 以非 0 退出码 {process.ExitCode} 退出！");
            }
        };

        process.OutputDataReceived += outputHandler;
        process.ErrorDataReceived += outputHandler;
        process.Exited += exitedHandler;

        await using var cancellationRegistration = cancellationToken.Register(() =>
        {
            completionSource.TrySetCanceled(cancellationToken);
        });

        try
        {
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            await completionSource.Task;
        }
        finally
        {
            process.OutputDataReceived -= outputHandler;
            process.ErrorDataReceived -= outputHandler;
            process.Exited -= exitedHandler;
        }
    }
}

internal enum GameStatus
{
    Waiting = 0,
    LogOutput = 1,
    SetUser = 2,
    LwjglLoaded = 3,
    OpenALLoaded = 4,
    TexturesLoaded = 5
}
