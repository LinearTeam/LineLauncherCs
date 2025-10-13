namespace LMCCore.Account.Model;

public class MicrosoftAccount : Account
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string AccessToken { get; set; } = string.Empty; // 不存于文件内

    public string RefreshToken { get; set; } = string.Empty;

    public MicrosoftAccount()
    {
        Type = AccountType.Microsoft;
    }
}
