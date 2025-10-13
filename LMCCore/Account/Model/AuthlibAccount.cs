namespace LMCCore.Account.Model;

public class AuthlibAccount : Account
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AuthServer { get; set; } = string.Empty;

    public AuthlibAccount()
    {
        Type = AccountType.Authlib;
    }
}
