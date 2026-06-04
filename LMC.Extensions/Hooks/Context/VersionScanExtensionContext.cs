namespace LMC.Extensions.Hooks.Context;

public class VersionScanExtensionContext
{
    public required string RootPath { get; init; }

    public required IReadOnlyList<string> VersionIds { get; init; }
}
