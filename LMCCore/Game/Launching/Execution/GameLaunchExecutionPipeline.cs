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

using LMC.Basic.Logging;
using LMCCore.Game.Launching.Steps;

namespace LMCCore.Game.Launching.Execution;

internal sealed class GameLaunchExecutionPipeline(
    IEnumerable<IGameLaunchStepHandler> stepHandlers,
    Logger logger)
{
    private readonly IReadOnlyList<IGameLaunchStepHandler> _stepHandlers = stepHandlers.ToList().AsReadOnly();
    private readonly Logger _logger = logger;

    public async Task<GameLaunchResult> ExecuteAsync(
        GameLaunchContext context,
        IProgress<GameLaunchProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var stepHandler in _stepHandlers.OrderBy(handler => (int)handler.Step))
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new GameLaunchProgress
            {
                Step = stepHandler.Step,
                StepIndex = (int)stepHandler.Step,
                TotalSteps = _stepHandlers.Count
            });
            _logger.Info($"Executing launch step '{stepHandler.Step}' for version '{context.Version.VersionName}'.");
            await stepHandler.ExecuteAsync(context, cancellationToken);
        }

        return new GameLaunchResult
        {
            Process = context.Process,
            WindowDetected = context.WindowDetected
        };
    }
}
