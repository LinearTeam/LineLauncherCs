using System.Text.Json.Serialization;
using LMC;
using LMCCore.Account.Model;
using System.Text.Json;

namespace LMCCore.Account;


public static class AccountManager
{
    private static readonly string s_accountsPath = Path.Combine(Current.LMCPath, "accounts.json");
    private static List<Model.Account> s_accounts = new List<Model.Account>();

    public static IReadOnlyList<Model.Account> Accounts => s_accounts.AsReadOnly();

    public static void Load()
    {
        if (!File.Exists(s_accountsPath))
        {
            s_accounts = new List<Model.Account>();
            return;
        }
        var json = File.ReadAllText(s_accountsPath);
        s_accounts = JsonSerializer.Deserialize<List<Model.Account>>(json, new JsonSerializerOptions
        {
            Converters = { new AccountJsonConverter() }
        }) ?? new List<Model.Account>();
    }

    public static void Save()
    {
        var json = JsonSerializer.Serialize(s_accounts, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new AccountJsonConverter() }
        });
        File.WriteAllText(s_accountsPath, json);
    }

    public static void Add(Model.Account account)
    {
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
