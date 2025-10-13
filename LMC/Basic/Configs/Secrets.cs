namespace LMC.Basic.Configs;

using System.Collections.Generic;

public class Secrets
{
    public string Token { get; set; } = string.Empty;
    public Dictionary<string, string> Extra { get; set; } = new();
}