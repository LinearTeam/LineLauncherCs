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
using System;
using System.IO;

namespace LMCUI.Pages.AccountPage;

internal static class AccountAvatarCacheHelper
{
    public static bool ShouldUseCachedAvatar(string? cachedSkinUrl, string? currentSkinUrl, bool hasCachedAvatar)
    {
        return hasCachedAvatar &&
               !string.IsNullOrWhiteSpace(currentSkinUrl) &&
               string.Equals(cachedSkinUrl, currentSkinUrl, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetAvatarCachePath(string cacheDirectory, string uuid)
    {
        return Path.Combine(cacheDirectory, $"{NormalizeUuid(uuid)}.png");
    }

    public static string GetSkinUrlCachePath(string cacheDirectory, string uuid)
    {
        return Path.Combine(cacheDirectory, $"{NormalizeUuid(uuid)}.url");
    }

    public static string NormalizeUuid(string uuid)
    {
        return uuid.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
    }

    public static string ToDataUri(byte[] pngBytes)
    {
        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
    }
}
