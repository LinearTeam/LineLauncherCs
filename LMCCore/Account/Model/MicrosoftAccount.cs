using System.Text.Json.Serialization;

namespace LMCCore.Account.Model;

public class MicrosoftAccount : Account
{
    [JsonIgnore]
    public string AccessToken { get; set; } = string.Empty; // 不存于文件内

    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }

    public MicrosoftAccount()
    {
        Type = AccountType.Microsoft;
    }
}
