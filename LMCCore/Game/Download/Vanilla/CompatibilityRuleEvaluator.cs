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

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LMCCore.Game.Model.LocalVersion.Compatibility;

namespace LMCCore.Game.Download.Vanilla;

/// <summary>
/// 兼容性规则评估器
/// </summary>
public static class CompatibilityRuleEvaluator
{
    /// <summary>
    /// 检查规则是否适用于当前平台。
    /// 平台规则优先于没有 os 的默认规则；没有 allow 规则时，未匹配平台默认允许。
    /// </summary>
    public static bool CheckRulesApply(List<CompatibilityRule>? rules)
    {
        if (rules is not { Count: > 0 })
            return true;

        bool? platformDecision = null;
        bool? defaultDecision = null;
        var hasAllowRule = false;

        foreach (var rule in rules)
        {
            var isAllow = string.Equals(rule.Action, "allow", StringComparison.OrdinalIgnoreCase);
            var isDisallow = string.Equals(rule.Action, "disallow", StringComparison.OrdinalIgnoreCase);
            if (!isAllow && !isDisallow)
                continue;

            hasAllowRule |= isAllow;
            if (rule.Os is null)
            {
                defaultDecision = isAllow;
            }
            else if (RuleMatchesOs(rule))
            {
                platformDecision = isAllow;
            }
        }

        return platformDecision ?? defaultDecision ?? !hasAllowRule;
    }

    /// <summary>
    /// 检查规则中的操作系统、版本和架构是否匹配当前环境。
    /// </summary>
    public static bool RuleMatchesOs(CompatibilityRule rule)
    {
        if (rule.Os is null)
            return true;

        if (!string.IsNullOrWhiteSpace(rule.Os.Name))
        {
            var os = PlatformDetector.ParseOsName(rule.Os.Name);
            if (!os.HasValue || !IsOsMatch(os.Value))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.Os.Version))
        {
            var osVersion = Environment.OSVersion.Version;
            var osVersionText = $"{osVersion.Major}.{osVersion.Minor}.{osVersion.Build}";
            try
            {
                if (!Regex.IsMatch(osVersionText, rule.Os.Version, RegexOptions.CultureInvariant))
                    return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        if (rule.Os.VersionRange is not null)
        {
            var osVersion = Environment.OSVersion.Version;
            var range = rule.Os.VersionRange;
            if (!string.IsNullOrWhiteSpace(range.Min))
            {
                if (!Version.TryParse(range.Min, out var minVersion) || osVersion < minVersion)
                    return false;
            }

            if (!string.IsNullOrWhiteSpace(range.Max))
            {
                if (!Version.TryParse(range.Max, out var maxVersion) || osVersion > maxVersion)
                    return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(rule.Os.Arch))
        {
            try
            {
                if (!Regex.IsMatch(
                        PlatformDetector.GetCurrentArchitectureName(),
                        $"^(?:{rule.Os.Arch})$",
                        RegexOptions.CultureInvariant))
                {
                    return false;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOsMatch(OSPlatform targetOs)
    {
        return RuntimeInformation.IsOSPlatform(targetOs);
    }
}
