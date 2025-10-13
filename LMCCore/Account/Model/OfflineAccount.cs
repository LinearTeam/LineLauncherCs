namespace LMCCore.Account.Model;

public class OfflineAccount : Account
{
    public OfflineAccount()
    {
        Type = AccountType.Offline;
    }
}
