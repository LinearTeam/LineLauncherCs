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
    /// 检查规则是否适用于当前平台
    /// </summary>
    public static bool CheckRulesApply(List<CompatibilityRule>? rules)
    {
        if (rules == null)
            return true;

        var otherOsAllowed = false;
        foreach (var rule in rules)
        {
            if (RuleMatchesOs(rule))
            {
                switch (rule.Action)
                {
                    case "disallow":
                        return false;

                    case "allow":
                        return true;

                    default:
                        continue;
                }
            }
            else if (rule.Action == "allow")
            {
                otherOsAllowed = true;
            }
        }
        return !otherOsAllowed;
    }

    /// <summary>
    /// 检查规则是否匹配当前操作系统
    /// </summary>
    public static bool RuleMatchesOs(CompatibilityRule rule)
    {
        if (!string.IsNullOrEmpty(rule.Os?.Name))
        {
            var name = rule.Os.Name;
            if (name.Contains('-'))
            {
                var parts = name.Split('-', 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var osNullable = PlatformDetector.ParseOsName(parts[0]);
                    var archNullable = PlatformDetector.ParseArchName(parts[1]);
                    if (osNullable.HasValue && archNullable.HasValue)
                    {
                        var os = osNullable.Value;
                        var arch = archNullable.Value;
                        return IsOsMatch(os) && arch == RuntimeInformation.OSArchitecture;
                    }
                }
            }
            var osNullable2 = PlatformDetector.ParseOsName(rule.Os.Name);
            if (!osNullable2.HasValue) return false;
            if (!IsOsMatch(osNullable2.Value))
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(rule.Os?.Version))
        {
            var osVersion = Environment.OSVersion.Version;
            var osVersionStr = $"{osVersion.Major}.{osVersion.Minor}.{osVersion.Build}";
            if (!Regex.IsMatch(osVersionStr, rule.Os.Version))
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(rule.Os?.Arch))
        {
            var archName = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x86_64",
                Architecture.Arm => "arm32",
                _ => RuntimeInformation.OSArchitecture.ToString().ToLower()
            };
            if (!Regex.IsMatch(archName, rule.Os.Arch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsOsMatch(OSPlatform targetOs)
    {
        if (RuntimeInformation.IsOSPlatform(targetOs))
            return true;

        // Linux 特殊处理：FreeBSD 也算 Linux
        if (targetOs == OSPlatform.Linux &&
            (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux)))
        {
            return true;
        }

        return false;
    }
}
