namespace LMC.Basic.Configs;

using System.Collections.Generic;

public class Secrets
{
    public string Token { get; set; } = string.Empty;
    public Dictionary<string, string> Extra { get; set; } = new();
}
/*
 * 这里描述Secrets中Extra的结构:
 * {
 *   Accounts: [] // List<Account>的Json序列化结果
 * }
 *
 *
 * 
 */