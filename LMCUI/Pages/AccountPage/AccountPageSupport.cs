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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LMCCore.Account.Model;

namespace LMCUI.Pages.AccountPage;

internal sealed record AccountListPresentation(string DescriptionKey, IReadOnlyList<Account> Accounts);

internal sealed record MicrosoftAvatarRefreshSummary(bool HasAvatarUpdate, bool ShouldSaveAccounts);

internal static class AccountPageSupport
{
    public static AccountListPresentation BuildAccountListPresentation(IEnumerable<Account> accounts)
    {
        var snapshot = accounts.ToList();
        var descriptionKey = snapshot.Count == 0
            ? "Pages.AccountPage.AccountListExpander.NoAccountsDescription"
            : "Pages.AccountPage.AccountListExpander.Description";

        return new AccountListPresentation(descriptionKey, snapshot);
    }

    public async static Task<MicrosoftAvatarRefreshSummary> RefreshMicrosoftAvatarsAsync(
        IEnumerable<MicrosoftAccount> accounts,
        Func<MicrosoftAccount, CancellationToken, Task<bool>> refreshAvatarAsync,
        CancellationToken cancellationToken = default)
    {
        var hasAvatarUpdate = false;
        var shouldSaveAccounts = false;

        foreach (var account in accounts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var previousRefreshToken = account.RefreshToken;
            var avatarUpdated = await refreshAvatarAsync(account, cancellationToken);
            hasAvatarUpdate |= avatarUpdated;
            shouldSaveAccounts |= !string.Equals(previousRefreshToken, account.RefreshToken, StringComparison.Ordinal);
        }

        return new MicrosoftAvatarRefreshSummary(hasAvatarUpdate, shouldSaveAccounts);
    }
}
