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

using System.Net;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using LMCCore.Account.Model;
using LMCCore.Utils;

namespace LMCCore.Account.OAuth;

public static class MicrosoftOAuth
{
    public const string LoginUrl = "https://blog.huangyu.win/line/loginRedirect.html?url=https%3a%2f%2flogin.microsoftonline.com%2fconsumers%2foauth2%2fv2.0%2fauthorize%3fclient_id%3d1cbfda79-fc84-47f9-8110-f924da9841ec%26response_type%3dcode%26redirect_uri%3dhttps%3a%2f%2fblog.huangyu.win%2fline%2floginSuccess.html%3f%26response_mode%3dquery%26scope%3dXboxLive.signin%20offline_access";
    
    private readonly static Logger s_logger = new Logger("MSOAuth");
    private static HttpListener? s_listener;
    private static CancellationTokenSource? s_cancellationTokenSource;
    

    public static void CancelOAuth()
    {
        try
        {
            s_cancellationTokenSource?.Cancel();
            s_listener?.Stop();
            s_logger.Info("OAuth验证已取消");
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "取消OAuth验证时发生错误");
        }
    }

    public async static Task<MicrosoftAccount?> StartOAuth(Action<OAuthReport> reportAction)
    {
        s_cancellationTokenSource = new CancellationTokenSource();
        s_logger.Info("开始微软登录");
        var coordinator = new MicrosoftOAuthFlowCoordinator(
            s_logger,
            new OAuthProgressReporter(reportAction),
            new MicrosoftOAuthFlowDependencies
            {
                GetAuthCodeAsync = GetAuthCode,
                GetTokenByAuthCodeAsync = GetTokenByAuthCode,
                GetXblTokenAsync = GetXblToken,
                GetXstsTokenAsync = GetXstsToken,
                GetMinecraftAccessTokenAsync = GetMinecraftAccessToken,
                CheckMinecraftOwnershipAsync = CheckMinecraftOwnership
            });
        return await coordinator.StartAsync(s_cancellationTokenSource.Token);
    }
    
    internal static async Task<OAuthOperationResult<MinecraftOwnershipPayload>> CheckMinecraftOwnership(string mcAccessToken)
    {
        try
        {
            var response = await HttpUtils.CreateRequest("https://api.minecraftservices.com/entitlements/mcstore")
                .WithHeader("Authorization", "Bearer " + mcAccessToken)
                .GetAsync();
            var responseString = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var json = JsonUtils.Parse(responseString);
            var items = json.GetArray<object>("items");
            bool haveMc = items is { Count: > 0 };
            if (!haveMc)
            {
                return OAuthOperationResult<MinecraftOwnershipPayload>.Success(
                    new MinecraftOwnershipPayload(false, null, null));
            }
            var profileResponse = await HttpUtils.CreateRequest("https://api.minecraftservices.com/minecraft/profile")
                .WithHeader("Authorization", "Bearer " + mcAccessToken)
                .GetAsync();
            var profileResponseString = await profileResponse.Content.ReadAsStringAsync();
            profileResponse.EnsureSuccessStatusCode();
            return OAuthOperationResult<MinecraftOwnershipPayload>.Success(
                MicrosoftOAuthParser.ParseOwnership(responseString, profileResponseString));
        }catch (Exception ex)
        {
            return OAuthOperationResult<MinecraftOwnershipPayload>.Failure(ex);
        }
    }

    public async static Task<(string? skinUrl, Exception? exception)> GetActiveSkinUrlAsync(MicrosoftAccount account, CancellationToken cancellationToken = default)
    {
        try
        {
            s_logger.Info($"开始获取微软账号活跃皮肤地址: {account.Name}");
            var mcAccessTokenResult = await GetMinecraftServiceAccessTokenAsync(account, cancellationToken);
            if (mcAccessTokenResult.exception != null || string.IsNullOrWhiteSpace(mcAccessTokenResult.accessToken))
            {
                s_logger.Warn($"获取微软账号 Minecraft AccessToken 失败: {account.Name}");
                return (null, mcAccessTokenResult.exception ?? new Exception("Failed to get minecraft access token"));
            }

            var profileResponse = await HttpUtils.CreateRequest("https://api.minecraftservices.com/minecraft/profile")
                .WithHeader("Authorization", "Bearer " + mcAccessTokenResult.accessToken)
                .GetAsync(cancellationToken);
            var profileResponseString = await profileResponse.Content.ReadAsStringAsync(cancellationToken);
            profileResponse.EnsureSuccessStatusCode();

            var activeSkinUrl = MicrosoftOAuthParser.SelectActiveSkinUrl(profileResponseString);
            if (string.IsNullOrWhiteSpace(activeSkinUrl))
            {
                s_logger.Info($"微软账号没有可用皮肤记录: {account.Name}");
                return (null, null);
            }

            s_logger.Info($"已获取微软账号活跃皮肤地址: {account.Name}");
            return (activeSkinUrl, null);
        }
        catch (Exception ex)
        {
            s_logger.Warn($"获取微软账号活跃皮肤地址失败: {account.Name}");
            return (null, ex);
        }
    }
    
    internal static async Task<OAuthOperationResult<string>> GetMinecraftAccessToken(string userHash, string xstsToken, CancellationToken cancellationToken)
    {
        try
        {
            var response = await HttpUtils.CreateRequest("https://api.minecraftservices.com/authentication/login_with_xbox")
                .WithJsonContent(new
                {
                    identityToken = $"XBL3.0 x={userHash};{xstsToken}"
                })
                .PostAsync(cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = JsonUtils.Parse(responseString);
            var token = json.GetString("access_token");
            return OAuthOperationResult<string>.Success(token!);
        }catch (Exception ex)
        {
            return OAuthOperationResult<string>.Failure(ex);
        }
    }

    public async static Task<(string? accessToken, Exception? exception)> GetMinecraftServiceAccessTokenAsync(MicrosoftAccount account, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(account.AccessToken) && account.ExpiresAt > DateTimeOffset.Now.AddMinutes(1))
            {
                var existingTokenResult = await TryGetMinecraftServiceAccessTokenFromAccessTokenAsync(account, cancellationToken);
                if (existingTokenResult.accessToken != null || existingTokenResult.exception == null)
                {
                    return existingTokenResult;
                }

                s_logger.Warn($"微软账号现有 AccessToken 不可用，准备使用 RefreshToken: {account.Name}");
            }

            s_logger.Info($"微软账号开始使用 RefreshToken 刷新令牌: {account.Name}");
            var refreshResult = await RefreshMicrosoftAccountTokenAsync(account, cancellationToken);
            if (refreshResult.exception != null)
            {
                s_logger.Warn($"微软账号 RefreshToken 刷新失败: {account.Name}");
                return refreshResult;
            }

            s_logger.Info($"微软账号令牌刷新成功: {account.Name}");
            return await TryGetMinecraftServiceAccessTokenFromAccessTokenAsync(account, cancellationToken);
        }
        catch (Exception ex)
        {
            s_logger.Warn($"微软账号获取 Minecraft 服务令牌失败: {account.Name}");
            return (null, ex);
        }
    }

    private static async Task<(string? accessToken, Exception? exception)> TryGetMinecraftServiceAccessTokenFromAccessTokenAsync(
        MicrosoftAccount account,
        CancellationToken cancellationToken)
    {
        s_logger.Info($"微软账号使用现有 AccessToken 获取 Minecraft 服务令牌: {account.Name}");
        var xblResult = await GetXblToken(account.AccessToken, cancellationToken);
        if (!xblResult.IsSuccess || string.IsNullOrWhiteSpace(xblResult.Value?.Token))
        {
            return (null, xblResult.Exception ?? new Exception("Failed to get xbl token"));
        }

        var xstsResult = await GetXstsToken(xblResult.Value.Token!, cancellationToken);
        if (!xstsResult.IsSuccess ||
            string.IsNullOrWhiteSpace(xstsResult.Value?.Token) ||
            string.IsNullOrWhiteSpace(xstsResult.Value?.UserHash))
        {
            return (null, xstsResult.Exception ?? new Exception("Failed to get xsts token"));
        }

        s_logger.Info($"微软账号现有 AccessToken 可用: {account.Name}");
        var minecraftAccessToken = await GetMinecraftAccessToken(
            xstsResult.Value.UserHash!,
            xstsResult.Value.Token!,
            cancellationToken);
        return minecraftAccessToken.IsSuccess
            ? (minecraftAccessToken.Value, null)
            : (null, minecraftAccessToken.Exception);
    }

    private static async Task<(string? accessToken, Exception? exception)> RefreshMicrosoftAccountTokenAsync(
        MicrosoftAccount account,
        CancellationToken cancellationToken)
    {
        var tokenResult = await GetTokenByRefreshToken(account.RefreshToken, cancellationToken);
        if (!tokenResult.IsSuccess || string.IsNullOrWhiteSpace(tokenResult.Value?.AccessToken))
        {
            return (null, tokenResult.Exception ?? new Exception("Failed to refresh microsoft access token"));
        }

        account.AccessToken = tokenResult.Value.AccessToken!;
        if (!string.IsNullOrWhiteSpace(tokenResult.Value.RefreshToken))
        {
            account.RefreshToken = tokenResult.Value.RefreshToken!;
        }

        account.ExpiresAt = DateTimeOffset.Now.AddSeconds(tokenResult.Value.ExpiresIn);
        return (account.AccessToken, null);
    }
    
    internal static async Task<OAuthOperationResult<XboxTokenPayload>> GetXstsToken(string xblToken, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await HttpUtils.CreateRequest("https://xsts.auth.xboxlive.com/xsts/authorize")
                .WithJsonContent(new
                {
                    Properties = new
                    {
                        SandboxId = "RETAIL",
                        UserTokens = new[] { xblToken }
                    },
                    RelyingParty = "rp://api.minecraftservices.com/",
                    TokenType = "JWT"
                })
                .PostAsync(cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();
            return OAuthOperationResult<XboxTokenPayload>.Success(
                MicrosoftOAuthParser.ParseXboxTokenPayload(responseString));
        }catch (Exception ex)
        {
            return OAuthOperationResult<XboxTokenPayload>.Failure(ex);
        }
    }
    
    internal static async Task<OAuthOperationResult<XboxTokenPayload>> GetXblToken(string accessToken, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rpsTicket = attempt == 0 ? $"d={accessToken}" : accessToken;
                var response = await HttpUtils.CreateRequest("https://user.auth.xboxlive.com/user/authenticate")
                    .WithJsonContent(new
                    {
                        Properties = new
                        {
                            AuthMethod = "RPS",
                            SiteName = "user.auth.xboxlive.com",
                            RpsTicket = rpsTicket
                        },
                        RelyingParty = "http://auth.xboxlive.com",
                        TokenType = "JWT"
                    })
                    .PostAsync(cancellationToken);
                var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
                response.EnsureSuccessStatusCode();
                return OAuthOperationResult<XboxTokenPayload>.Success(
                    MicrosoftOAuthParser.ParseXboxTokenPayload(responseString));
            }
            catch (Exception ex) when (attempt == 0 && ex.Message.Contains("400"))
            {
                continue;
            }
            catch (Exception ex)
            {
                return OAuthOperationResult<XboxTokenPayload>.Failure(ex);
            }
        }
        return OAuthOperationResult<XboxTokenPayload>.Failure(new Exception("XBL token acquisition failed after retries"));
    }
    
    internal static async Task<OAuthOperationResult<OAuthTokenPayload>> GetTokenByAuthCode(string code, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await HttpUtils.CreateRequest("https://login.microsoftonline.com/consumers/oauth2/v2.0/token")
                .WithFormContent(builder => builder
                    .Add("client_id", "1cbfda79-fc84-47f9-8110-f924da9841ec")
                    .Add("code", code)
                    .Add("grant_type", "authorization_code")
                    .Add("redirect_uri", "https://blog.huangyu.win/line/loginSuccess.html?")
                    .Add("scope", "XboxLive.signin offline_access"))
                .PostAsync(cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                if (response.StatusCode is >= HttpStatusCode.BadRequest and <= HttpStatusCode.InternalServerError)
                {
                    s_logger.Error("Failed to get Access Token, response: \n" + responseString);
                }
                throw;
            }
            return OAuthOperationResult<OAuthTokenPayload>.Success(
                MicrosoftOAuthParser.ParseTokenPayload(responseString));
        }catch (Exception ex)
        {
            return OAuthOperationResult<OAuthTokenPayload>.Failure(ex);
        }
    }

    internal static async Task<OAuthOperationResult<OAuthTokenPayload>> GetTokenByRefreshToken(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await HttpUtils.CreateRequest("https://login.microsoftonline.com/consumers/oauth2/v2.0/token")
                .WithFormContent(builder => builder
                    .Add("client_id", "1cbfda79-fc84-47f9-8110-f924da9841ec")
                    .Add("refresh_token", refreshToken)
                    .Add("grant_type", "refresh_token")
                    .Add("scope", "XboxLive.signin offline_access"))
                .PostAsync(cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();
            return OAuthOperationResult<OAuthTokenPayload>.Success(
                MicrosoftOAuthParser.ParseTokenPayload(responseString));
        }
        catch (Exception ex)
        {
            return OAuthOperationResult<OAuthTokenPayload>.Failure(ex);
        }
    }
    
    

    async private static Task<(string? code, Exception? exception)> GetAuthCode(CancellationToken cancellationToken)
    {
        try
        {
            s_listener = OAuthCallbackListener.Create();
            var result = await OAuthCallbackListener.WaitForCodeAsync(s_listener, cancellationToken);
            s_listener.Stop();
            return result;
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }
}

public record OAuthReport(int Step, int TotalStep, string Message);
