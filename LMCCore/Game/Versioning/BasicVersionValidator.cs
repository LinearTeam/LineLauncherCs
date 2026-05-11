using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;
using LMCCore.Utils;

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

        if (!string.IsNullOrWhiteSpace(version.JsonPath) && File.Exists(version.JsonPath))
        {
            JsonUtils parsedJson;
            try
            {
                parsedJson = JsonUtils.Parse(File.ReadAllText(version.JsonPath));
            }
            catch (Exception ex)
            {
                issues.Add(new VersionValidationIssue
                {
                    Code = "json_read_failed",
                    Message = $"读取版本 JSON 失败: {ex.Message}",
                    Severity = VersionValidationSeverity.Error
                });

                return Task.FromResult(ToResult(issues));
            }

            if (!parsedJson.IsValid)
            {
                issues.Add(new VersionValidationIssue
                {
                    Code = "json_invalid",
                    Message = "版本 JSON 无法解析。",
                    Severity = VersionValidationSeverity.Error
                });
            }
            else
            {
                var versionInfo = parsedJson.Get<LocalVersionInfo>();
                if (versionInfo == null)
                {
                    issues.Add(new VersionValidationIssue
                    {
                        Code = "json_schema_invalid",
                        Message = "版本 JSON 无法反序列化为本地版本信息。",
                        Severity = VersionValidationSeverity.Error
                    });
                }
            }
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
