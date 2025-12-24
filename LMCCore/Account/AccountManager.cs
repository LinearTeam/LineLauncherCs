using System.Text;
using System.Text.Json.Serialization;
using LMC;
using LMCCore.Account.Model;
using System.Text.Json;
using System.Security.Cryptography;
using LMC.Basic.Configs;
using LMCCore.Utils;

namespace LMCCore.Account;


public static class AccountManager
{
    private static List<Model.Account> s_accounts = new List<Model.Account>();

    public static IReadOnlyList<Model.Account> Accounts = s_accounts.AsReadOnly();
    public static string GenerateOfflineUuid(string playerName)
    {
        string input = "OfflinePlayer:" + playerName;
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);

        byte[] hashBytes;
        using (MD5 md5 = MD5.Create())
        {
            hashBytes = md5.ComputeHash(inputBytes);
        }

        // UUID版本3 和变体 RFC
        hashBytes[6] = (byte)((hashBytes[6] & 0x0F) | 0x30); 
        hashBytes[8] = (byte)((hashBytes[8] & 0x3F) | 0x80); 

        byte[] reordered = new byte[16];
        reordered[0] = hashBytes[3];
        reordered[1] = hashBytes[2];
        reordered[2] = hashBytes[1];
        reordered[3] = hashBytes[0];
        reordered[4] = hashBytes[5];
        reordered[5] = hashBytes[4];
        reordered[6] = hashBytes[7];
        reordered[7] = hashBytes[6];
        Array.Copy(hashBytes, 8, reordered, 8, 8); // 最后8个字节直接复制

        return new Guid(reordered).ToString();
    }

    public static void Load()
    {
        var accStr = SecretsManager.Instance.Extra.GetValueOrDefault("Accounts", "[]");
        var accounts = JsonUtils.Parse(accStr).GetArray<Model.Account>();
        s_accounts = (accounts ?? []).ToList();
        Accounts = s_accounts.AsReadOnly();
    }

    public static void Save()
    {
        SecretsManager.Instance.Extra["Accounts"] = JsonSerializer.Serialize(s_accounts, JsonUtils.DefaultSerializeOptions);
        SecretsManager.Save();
    }

    public static void Add(Model.Account account)
    {
        // 检查是否已存在相同账户
        bool isDuplicate = s_accounts.Any(existingAccount =>
        {
            if (existingAccount.Type != account.Type)
                return false;
                
            switch (account.Type)
            {
                case AccountType.Offline:
                    return existingAccount.Name == account.Name;
                    
                case AccountType.Microsoft:
                    string normalizedExistingUuid = existingAccount.Uuid.Replace("-", "").ToLower();
                    string normalizedNewUuid = account.Uuid.Replace("-", "").ToLower();
                    return normalizedExistingUuid == normalizedNewUuid;
                    
                case AccountType.Authlib:
                    if (existingAccount is AuthlibAccount authlibAccount && account is AuthlibAccount addAuthlibAccount)
                    {
                        return authlibAccount.Username.Equals(addAuthlibAccount.Username, StringComparison.OrdinalIgnoreCase);
                    }
                    throw new Exception($"Account type is not Authlib (exist: {existingAccount is AuthlibAccount}, add: {account is AuthlibAccount})");
                    
                default:
                    return false;
            }
        });
        
        if (isDuplicate)
        {
            throw new ArgumentException("Messages.AccountManager.AddAccount.Duplicate");
        }
        
        s_accounts.Add(account);
        Save();
    }

    public static void Remove(Model.Account account)
    {
        s_accounts.Remove(account);
        Save();
    }
}
public class AccountJsonConverter : JsonConverter<Model.Account>
{
    public override Model.Account? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var type = (AccountType)root.GetProperty("Type").GetInt32();
        return type switch
        {
            AccountType.Offline => JsonSerializer.Deserialize<OfflineAccount>(root.GetRawText(), options),
            AccountType.Microsoft => JsonSerializer.Deserialize<MicrosoftAccount>(root.GetRawText(), options),
            AccountType.Authlib => JsonSerializer.Deserialize<AuthlibAccount>(root.GetRawText(), options),
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, Model.Account value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case OfflineAccount offline:
                JsonSerializer.Serialize(writer, offline, options);
                break;
            case MicrosoftAccount ms:
                JsonSerializer.Serialize(writer, ms, options);
                break;
            case AuthlibAccount authlib:
                JsonSerializer.Serialize(writer, authlib, options);
                break;
        }
    }
}
