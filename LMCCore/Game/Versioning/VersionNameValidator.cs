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

namespace LMCCore.Game.Versioning;

public static class VersionNameValidator
{
    public static bool IsValid(string? versionName)
    {
        if (string.IsNullOrWhiteSpace(versionName) || versionName is "." or "..")
        {
            return false;
        }

        if (versionName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            versionName.Contains(Path.DirectorySeparatorChar) ||
            versionName.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        if (versionName.EndsWith(' ') || versionName.EndsWith('.'))
        {
            return false;
        }

        var deviceName = Path.GetFileNameWithoutExtension(versionName);
        if (deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return deviceName.Length != 4 ||
               (!deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) &&
                !deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) ||
               !char.IsAsciiDigit(deviceName[3]) ||
               deviceName[3] == '0';
    }
}
