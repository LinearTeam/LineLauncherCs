namespace LMCCore.Game.Model;

public class VersionValidationResult
{
    public required bool IsValid { get; init; }

    public required IReadOnlyList<VersionValidationIssue> Issues { get; init; }
}
