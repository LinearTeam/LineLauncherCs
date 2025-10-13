namespace LMCCore.Account.Model;

public abstract class Account
{
    public AccountType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
}