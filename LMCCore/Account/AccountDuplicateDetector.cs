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
