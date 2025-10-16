using System.Text.Json.Serialization;

namespace LMCCore.Account.Model;

public abstract class Account
{
    public AccountType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    
    [JsonIgnore]
    public string? AvatarBase64 { get; set; }
}
