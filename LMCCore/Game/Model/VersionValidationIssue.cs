namespace LMCCore.Game.Model;

public class VersionValidationIssue
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public required VersionValidationSeverity Severity { get; init; }
}
