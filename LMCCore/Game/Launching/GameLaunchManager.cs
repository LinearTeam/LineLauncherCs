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
using LMC.Basic.Logging;
using LMCCore.Game.Download;
using LMCCore.Game.Download.Vanilla;
using LMCCore.Game.Launching.Execution;
using LMCCore.Game.Launching.Providers;
using LMCCore.Game.Model;

namespace LMCCore.Game.Launching;

public class GameLaunchManager
{
    private readonly Logger _logger = new("GameLaunch");
    private readonly GameLaunchExecutionPipeline _pipeline;
    private readonly DownloadSourceManager _downloadSourceManager;
    private readonly VanillaGameDownloader _vanillaDownloader;

    public GameLaunchManager(
        IEnumerable<Steps.IGameLaunchStepHandler>? stepHandlers = null,
        DownloadSourceManager? downloadSourceManager = null)
    {
        _downloadSourceManager = downloadSourceManager ?? DownloadSourceManager.CreateDefault();
        _vanillaDownloader = new VanillaGameDownloader(_downloadSourceManager);
        _pipeline = new GameLaunchExecutionPipeline(
            stepHandlers ?? GameLaunchStepProvider.CreateDefault(),
            _logger);
    }

    public async virtual Task<GameLaunchResult> LaunchAsync(
        LocalGameVersionEntry version,
        AppConfig config,
        Account.Model.Account account,
        bool shouldValidateAndCompleteMissingFiles = true,
        IProgress<GameLaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(account);

        var context = new GameLaunchContext
        {
            Version = version,
            Config = config,
            DownloadSourceManager = _downloadSourceManager,
            VanillaDownloader = _vanillaDownloader,
            Account = account,
            ShouldValidateAndCompleteMissingFiles = shouldValidateAndCompleteMissingFiles
        };

        _logger.Info($"Start launching version '{version.VersionName}'.");
        return await _pipeline.ExecuteAsync(context, progress, cancellationToken);
    }
}
