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
             : (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) ? "linux"
             : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
             : "unknown";
    }

    /// <summary>
    /// 解析 OS 平台名称
    /// </summary>
    public static OSPlatform? ParseOsName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        if (name.Contains("win"))
        {
            return OSPlatform.Windows;
        }
        if (name.Contains("mac") || name.Contains("darwin") || name.Contains("osx"))
        {
            return OSPlatform.OSX;
        }
        if (name.Contains("solaris") || name.Contains("linux") || name.Contains("unix") || name.Contains("sunos"))
        {
            return OSPlatform.Linux;
        }
        if (name.Contains("freebsd"))
        {
            return OSPlatform.FreeBSD;
        }
        return null;
    }

    /// <summary>
    /// 解析架构名称
    /// </summary>
    public static Architecture? ParseArchName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        name = name.Trim().ToLower();

        return name switch
        {
            "x8664" or "x86-64" or "x86_64" or "amd64" or "ia32e" or "em64t" or "x64" or "intel64"
                => Architecture.X64,

            "x8632" or "x86-32" or "x86_32" or "x86" or "i86pc" or "i386" or "i486" or "i586" or "i686" or "ia32" or "x32"
                => Architecture.X86,

            "ppc64" or "powerpc64"
                => "little".Equals(Environment.GetEnvironmentVariable("sun.cpu.endian")) ? Architecture.Ppc64le : null,

            "ppc64le" or "powerpc64le"
                => Architecture.Ppc64le,

            "s390x"
                => Architecture.S390x,

            "arm64" or "aarch64"
                => Architecture.Arm64,

            "arm" or "arm32"
                => Architecture.Arm,

            "loongarch64"
                => IsLoongArch64Supported() ? Architecture.LoongArch64 : null,

            _ => ParseArchNameByPrefix(name)
        };
    }

    private static Architecture? ParseArchNameByPrefix(string name)
    {
        if (name.StartsWith("armv7"))
        {
            return Architecture.Arm;
        }
        if (name.StartsWith("armv8") || name.StartsWith("armv9"))
        {
            return Architecture.Arm64;
        }
        if (name.StartsWith("armv6"))
        {
            return Architecture.Armv6;
        }
        return null;
    }

    private static bool IsLoongArch64Supported()
    {
        var ver = Environment.OSVersion.Version;
        return ver.Major >= 5 && !(ver is { Major: 5, Minor: < 19 });
    }
}
