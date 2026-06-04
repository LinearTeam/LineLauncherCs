namespace LMC.Extensions.Hooks.Context;

public class ExtensionAccountInfo
{
    public required string Type { get; init; }

    public required string Name { get; init; }

    public string? Uuid { get; init; }

    public string? Username { get; init; }
}
