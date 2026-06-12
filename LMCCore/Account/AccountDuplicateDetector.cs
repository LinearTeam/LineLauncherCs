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
using LMCCore.Account.Model;

namespace LMCCore.Account;

internal static class AccountDuplicateDetector
{
    public static bool IsDuplicate(IEnumerable<Account.Model.Account> existingAccounts, Account.Model.Account account)
    {
        return existingAccounts.Any(existingAccount =>
        {
            if (existingAccount.Type != account.Type)
            {
                return false;
            }

            return account.Type switch
            {
                AccountType.Offline => existingAccount.Name == account.Name,
                AccountType.Microsoft => string.Equals(
                    NormalizeMicrosoftUuid(existingAccount.Uuid),
                    NormalizeMicrosoftUuid(account.Uuid),
                    StringComparison.Ordinal),
                AccountType.Authlib => existingAccount is AuthlibAccount authlibAccount &&
                                       account is AuthlibAccount addAuthlibAccount &&
                                       authlibAccount.Username.Equals(addAuthlibAccount.Username, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        });
    }

    private static string NormalizeMicrosoftUuid(string uuid)
    {
        return uuid.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
    }
}
