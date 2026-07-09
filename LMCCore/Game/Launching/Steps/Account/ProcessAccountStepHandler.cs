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
using LMCCore.Account;
using LMCCore.Account.Model;
using LMCCore.Account.OAuth;
using LMCCore.Game.Launching.Execution;

namespace LMCCore.Game.Launching.Steps.Account;

public sealed class ProcessAccountStepHandler : IGameLaunchStepHandler
{
    private readonly static Logger s_logger = new("ProcessAccountStep");

    public GameLaunchProgressStep Step => GameLaunchProgressStep.ProcessAccount;

    public async Task ExecuteAsync(GameLaunchContext context, CancellationToken cancellationToken)
    {
        if (context.Account is not MicrosoftAccount microsoftAccount)
        {
            return;
        }

        if (ShouldRefreshMicrosoftAccount(microsoftAccount))
        {
            s_logger.Info($"Refreshing expired Microsoft account token for '{microsoftAccount.Name}'.");
        }
        else
        {
            s_logger.Info($"Microsoft account token is still valid: {microsoftAccount.ExpiresAt:O}");
        }

        var refreshResult = await MicrosoftOAuth.GetMinecraftServiceAccessTokenAsync(
            microsoftAccount,
            cancellationToken);
        if (refreshResult.exception != null || string.IsNullOrWhiteSpace(refreshResult.accessToken))
        {
            throw refreshResult.exception ?? new InvalidOperationException(
                $"Failed to refresh Microsoft account token for '{microsoftAccount.Name}'.");
        }

        context.MinecraftAccessToken = refreshResult.accessToken;
        AccountManager.Save();
        s_logger.Info($"Minecraft access token prepared for '{microsoftAccount.Name}'.");
    }

    private static bool ShouldRefreshMicrosoftAccount(MicrosoftAccount account)
    {
        return string.IsNullOrWhiteSpace(account.AccessToken) ||
               account.ExpiresAt <= DateTimeOffset.Now.AddMinutes(1);
    }
}
