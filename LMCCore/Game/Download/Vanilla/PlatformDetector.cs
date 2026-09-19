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

namespace LMCCore.Game.Download.Vanilla;

/// <summary>
/// 平台检测工具类
/// </summary>
public static class PlatformDetector
{
    /// <summary>
    /// 获取当前操作系统的 Mojang 格式名称
    /// </summary>
    public static string GetCurrentOs()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
             : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
             : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux"
             : "unknown";
    }

    /// <summary>
    /// 解析 OS 平台名称
    /// </summary>
    public static OSPlatform? ParseOsName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        return name.Trim().ToLowerInvariant() switch
        {
            "windows" => OSPlatform.Windows,
            "osx" => OSPlatform.OSX,
            "linux" => OSPlatform.Linux,
            _ => null
        };
    }

    /// <summary>
    /// 解析架构名称
    /// </summary>
    public static Architecture? ParseArchName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var normalized = name.Trim().ToLowerInvariant();
        if (normalized == "x86" || normalized.Contains("x86", StringComparison.Ordinal))
            return Architecture.X86;

        if (normalized == "x64" || normalized.Contains("x64", StringComparison.Ordinal))
            return Architecture.X64;

        if (normalized.Contains("arm", StringComparison.Ordinal)
            && !normalized.Contains("64", StringComparison.Ordinal))
        {
            return Architecture.Arm;
        }

        if (normalized.Contains("arm", StringComparison.Ordinal)
            && normalized.Contains("64", StringComparison.Ordinal))
        {
            return Architecture.Arm64;
        }

        return null;
    }

    /// <summary>
    /// Returns the only architecture token required by Mojang native classifiers.
    /// </summary>
    public static string GetNativeArchitectureToken() => Environment.Is64BitProcess ? "64" : "32";

    /// <summary>
    /// Returns the canonical architecture name used when evaluating os.arch.
    /// </summary>
    public static string GetCurrentArchitectureName()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x86_64",
            Architecture.Arm => "arm32",
            Architecture.Arm64 => "arm64",
            _ => "unknown"
        };
    }
}
