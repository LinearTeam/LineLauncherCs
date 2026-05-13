using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Versioning;

public class BasicVersionValidator : IVersionValidator
{
    public Task<VersionValidationResult> ValidateAsync(LocalGameVersionEntry version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var issues = new List<VersionValidationIssue>();

        if (version.Status == VersionStatus.MissingJar)
        {
            issues.Add(new VersionValidationIssue
            {
                Code = "missing_jar",
                Message = "版本缺少必要的 JAR 文件。",
                Severity = VersionValidationSeverity.Error
            });
        }

        if (version.Status == VersionStatus.MissingJson)
        {
            issues.Add(new VersionValidationIssue
            {
                Code = "missing_json",
                Message = "版本缺少必要的 JSON 文件。",
                Severity = VersionValidationSeverity.Error
            });
        }

        if (version.Status == VersionStatus.InvalidJson)
        {
            issues.Add(new VersionValidationIssue
            {
                Code = "json_schema_invalid",
                Message = "版本 JSON 无法反序列化为本地版本信息。",
                Severity = VersionValidationSeverity.Error
            });
        }

        return Task.FromResult(ToResult(issues));
    }

    private static VersionValidationResult ToResult(IReadOnlyList<VersionValidationIssue> issues)
    {
        return new VersionValidationResult
        {
            IsValid = issues.All(issue => issue.Severity != VersionValidationSeverity.Error),
            Issues = issues
        };
    }
}
