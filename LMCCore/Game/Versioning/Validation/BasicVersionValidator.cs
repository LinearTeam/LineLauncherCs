// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
using LMCCore.Game.Model;
using LMCCore.Game.Model.Validation;
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Versioning.Validation;

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
