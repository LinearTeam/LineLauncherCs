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
