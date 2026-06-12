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
using System.Text.Json.Serialization;
using LMCCore.Account.Model;
using LMCCore.Utils;

namespace LMCCore.Account.OAuth;

internal sealed record OAuthOperationResult<T>(T? Value, Exception? Exception)
{
    public bool IsSuccess => Exception is null;

    public static OAuthOperationResult<T> Success(T value) => new(value, null);
    public static OAuthOperationResult<T> Failure(Exception exception) => new(default, exception);
}

internal sealed record OAuthTokenPayload(string? AccessToken, string? RefreshToken, int ExpiresIn);
internal sealed record XboxTokenPayload(string? Token, string? UserHash = null);
internal sealed record MinecraftOwnershipPayload(bool HasMinecraft, string? Uuid, string? Name);

internal static class MicrosoftOAuthParser
{
    public static OAuthTokenPayload ParseTokenPayload(string json)
    {
        if (!JsonUtils.TryDeserialize<TokenResponse>(json, out var response) || response == null)
        {
            throw new InvalidOperationException("Failed to parse microsoft token response");
        }

        return new OAuthTokenPayload(response.AccessToken, response.RefreshToken, response.ExpiresIn);
    }

    public static XboxTokenPayload ParseXboxTokenPayload(string json)
    {
        if (!JsonUtils.TryDeserialize<XboxTokenResponse>(json, out var response) || response == null)
        {
            throw new InvalidOperationException("Failed to parse xbox token response");
        }

        return new XboxTokenPayload(response.Token, response.DisplayClaims?.Xui?.FirstOrDefault()?.UserHash);
    }

    public static MinecraftOwnershipPayload ParseOwnership(string entitlementsJson, string profileJson)
    {
        if (!JsonUtils.TryDeserialize<MinecraftEntitlementsResponse>(entitlementsJson, out var entitlements) || entitlements == null)
        {
            throw new InvalidOperationException("Failed to parse minecraft entitlements response");
        }

        if (entitlements.Items == null || entitlements.Items.Count == 0)
        {
            return new MinecraftOwnershipPayload(false, null, null);
        }

        if (!JsonUtils.TryDeserialize<MinecraftProfileResponse>(profileJson, out var profile) || profile == null)
        {
            throw new InvalidOperationException("Failed to parse minecraft profile response");
        }

        var uuid = string.IsNullOrWhiteSpace(profile.Id) ? null : Guid.Parse(profile.Id).ToString();
        return new MinecraftOwnershipPayload(true, uuid, profile.Name);
    }

    public static string? SelectActiveSkinUrl(string profileJson)
    {
        if (!JsonUtils.TryDeserialize<MinecraftProfileResponse>(profileJson, out var profile) || profile == null)
        {
            throw new InvalidOperationException("Failed to parse minecraft profile response");
        }

        return SelectActiveSkinUrl(profile);
    }

    public static string? SelectActiveSkinUrl(MinecraftProfileResponse profile)
    {
        if (profile.Skins == null || profile.Skins.Count == 0)
        {
            return null;
        }

        return profile.Skins.FirstOrDefault(skin =>
                   string.Equals(skin.State, "ACTIVE", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(skin.Url))
                   ?.Url
               ?? profile.Skins.FirstOrDefault(skin => !string.IsNullOrWhiteSpace(skin.Url))?.Url;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; } = 3600;
    }

    private sealed class XboxTokenResponse
    {
        [JsonPropertyName("Token")]
        public string? Token { get; set; }

        [JsonPropertyName("DisplayClaims")]
        public XboxDisplayClaims? DisplayClaims { get; set; }
    }

    private sealed class XboxDisplayClaims
    {
        [JsonPropertyName("xui")]
        public List<XboxUserClaim>? Xui { get; set; }
    }

    private sealed class XboxUserClaim
    {
        [JsonPropertyName("uhs")]
        public string? UserHash { get; set; }
    }

    private sealed class MinecraftEntitlementsResponse
    {
        [JsonPropertyName("items")]
        public List<object>? Items { get; set; }
    }

    internal sealed class MinecraftProfileResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("skins")]
        public List<MinecraftSkinResponse>? Skins { get; set; }
    }

    internal sealed class MinecraftSkinResponse
    {
        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
