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
        int total = 6;
        int i = 1;
        s_cancellationTokenSource = new CancellationTokenSource();
        s_logger.Info("开始微软登录");
        reportAction(new OAuthReport(i++, total, "Messages.AccountManager.OAuth.Steps.WaitForCode.Message"));
        s_logger.Info($"进度：{i}/{total}");
        var codeResult = await GetAuthCode(s_cancellationTokenSource.Token);
        if(codeResult.exception != null)
        {
            if (s_cancellationTokenSource.IsCancellationRequested)
            {
                reportAction(new OAuthReport(-10, total, $"CANCEL: {codeResult.exception.Message}"));
                return null;
            }
            reportAction(new OAuthReport(-1, total, $"WAIT_FOR_CODE: {codeResult.exception.Message}"));
            s_logger.Error(codeResult.exception, $"获取授权码");
            return null;
        }
        var code = codeResult.code;
        SecretsManager.SensitiveData[code!] = "{OACode}";
        reportAction(new OAuthReport(i++, total, "Messages.AccountManager.OAuth.Steps.GetAccessToken.Message"));
        s_logger.Info($"进度：{i}/{total}");
        var tokenResult = await GetTokenByAuthCode(code!, s_cancellationTokenSource.Token);
        if (tokenResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"GET_ACCESS_TOKEN: {tokenResult.exception.Message}"));
            s_logger.Error(tokenResult.exception, $"获取访问令牌");
            return null;
        }
        reportAction(new OAuthReport(i++, total, "Messages.AccountManager.OAuth.Steps.XBLAuthorize.Message"));
        s_logger.Info($"进度：{i}/{total}");
        var xblResult = await GetXblToken(tokenResult.accessToken!, s_cancellationTokenSource.Token);
        if (xblResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"XBL_AUTHORIZE: {xblResult.exception.Message}"));
            s_logger.Error(xblResult.exception, $"获取XBL令牌");
            return null;
        }
        SecretsManager.SensitiveData[xblResult.xblToken!] = "{XBLToken}";
        reportAction(new OAuthReport(i++, total, "Messages.AccountManager.OAuth.Steps.XSTSAuthorize.Message"));
        var xstsResult = await GetXstsToken(xblResult.xblToken!, s_cancellationTokenSource.Token);
        if (xstsResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"XSTS_AUTHORIZE: {xstsResult.exception.Message}"));
            s_logger.Error(xstsResult.exception, $"获取XSTS令牌");
            return null;
        }
        SecretsManager.SensitiveData[xstsResult.xstsToken!] = "{XSTSToken}";
        SecretsManager.SensitiveData[xstsResult.userHash!] = "{UserHash}";
        s_logger.Info($"进度：{i}/{total}");
        reportAction(new OAuthReport(i++, total, "Messages.AccountManager.OAuth.Steps.MinecraftAuthorize.Message"));
        var mcResult = await GetMinecraftAccessToken(xstsResult.userHash!, xstsResult.xstsToken!, s_cancellationTokenSource.Token);
        if (mcResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"MINECRAFT_AUTHORIZE: {mcResult.exception.Message}"));
            s_logger.Error(mcResult.exception, $"获取Minecraft令牌");
            return null;
        }
        SecretsManager.SensitiveData[mcResult.accessToken!] = "{MCAccessToken}";
        s_logger.Info($"进度：{i}/{total}");
        reportAction(new OAuthReport(i++, total, "Messages.AccountManager.OAuth.Steps.ValidateMinecraft.Message"));
        var ownershipResult = await CheckMinecraftOwnership(mcResult.accessToken!);
        if (ownershipResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"VALIDATE_MINECRAFT: {ownershipResult.exception.Message}"));
            s_logger.Error(ownershipResult.exception, $"验证Minecraft拥有权");
            return null;
        }
        if (!ownershipResult.haveMc)
        {
            reportAction(new OAuthReport(-2, total, "该账户不拥有Minecraft"));
            s_logger.Info("该账户不拥有Minecraft");
            return null;
        }
        return new MicrosoftAccount(){
            AccessToken = tokenResult.accessToken!,
            RefreshToken = tokenResult.refreshToken!,
            ExpiresAt = DateTimeOffset.Now.AddSeconds(tokenResult.expiresIn),
            Type = AccountType.Microsoft,
            Name = ownershipResult.name!,
            Uuid = ownershipResult.uuid!
        };
    }
    
    async private static Task<(bool haveMc, string? uuid, string? name, Exception? exception)> CheckMinecraftOwnership(string mcAccessToken)
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
                return (false, null, null, null);
            }
            var profileResponse = await HttpUtils.CreateRequest("https://api.minecraftservices.com/minecraft/profile")
                .WithHeader("Authorization", "Bearer " + mcAccessToken)
                .GetAsync();
            var profileResponseString = await profileResponse.Content.ReadAsStringAsync();
            profileResponse.EnsureSuccessStatusCode();
            var profileJson = JsonUtils.Parse(profileResponseString);
            var uuid = Guid.Parse(profileJson.GetString("id")!);
            var name = profileJson.GetString("name");
            return (true, uuid.ToString(), name, null);
        }catch (Exception ex)
        {
            return (false, null, null, ex);
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

            var profileJson = JsonUtils.Parse(profileResponseString);
            var skins = profileJson.GetArray<object>("skins");
            if (skins == null || skins.Count == 0)
            {
                s_logger.Info($"微软账号没有可用皮肤记录: {account.Name}");
                return (null, null);
            }

            foreach (var skin in skins)
            {
                var skinJson = JsonUtils.Parse(System.Text.Json.JsonSerializer.Serialize(skin, JsonUtils.DefaultSerializeOptions));
                if (string.Equals(skinJson.GetString("state"), "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    var activeSkinUrl = skinJson.GetString("url");
                    s_logger.Info($"已获取微软账号活跃皮肤地址: {account.Name}");
                    return (activeSkinUrl, null);
                }
            }

            var firstSkinUrl = JsonUtils.Parse(System.Text.Json.JsonSerializer.Serialize(skins[0], JsonUtils.DefaultSerializeOptions))
                .GetString("url");
            s_logger.Info($"微软账号没有 ACTIVE 皮肤，回退到首个皮肤地址: {account.Name}");
            return (firstSkinUrl, null);
        }
        catch (Exception ex)
        {
            s_logger.Warn($"获取微软账号活跃皮肤地址失败: {account.Name}");
            return (null, ex);
        }
    }
    
    async private static Task<(string? accessToken, Exception? exception)> GetMinecraftAccessToken(string userHash, string xstsToken, CancellationToken cancellationToken)
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
            return (token, null);
        }catch (Exception ex)
        {
            return (null, ex);
        }
    }

    public async static Task<(string? accessToken, Exception? exception)> GetMinecraftServiceAccessTokenAsync(MicrosoftAccount account, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(account.AccessToken) && account.ExpiresAt > DateTimeOffset.Now.AddMinutes(1))
            {
                s_logger.Info($"微软账号使用现有 AccessToken 获取 Minecraft 服务令牌: {account.Name}");
                var xblFromAccessToken = await GetXblToken(account.AccessToken, cancellationToken);
                if (xblFromAccessToken.exception == null && !string.IsNullOrWhiteSpace(xblFromAccessToken.xblToken))
                {
                    var xstsFromAccessToken = await GetXstsToken(xblFromAccessToken.xblToken!, cancellationToken);
                    if (xstsFromAccessToken.exception == null &&
                        !string.IsNullOrWhiteSpace(xstsFromAccessToken.xstsToken) &&
                        !string.IsNullOrWhiteSpace(xstsFromAccessToken.userHash))
                    {
                        s_logger.Info($"微软账号现有 AccessToken 可用: {account.Name}");
                        return await GetMinecraftAccessToken(xstsFromAccessToken.userHash!, xstsFromAccessToken.xstsToken!, cancellationToken);
                    }
                }

                s_logger.Warn($"微软账号现有 AccessToken 不可用，准备使用 RefreshToken: {account.Name}");
            }

            s_logger.Info($"微软账号开始使用 RefreshToken 刷新令牌: {account.Name}");
            var tokenResult = await GetTokenByRefreshToken(account.RefreshToken, cancellationToken);
            if (tokenResult.exception != null || string.IsNullOrWhiteSpace(tokenResult.accessToken))
            {
                s_logger.Warn($"微软账号 RefreshToken 刷新失败: {account.Name}");
                return (null, tokenResult.exception ?? new Exception("Failed to refresh microsoft access token"));
            }

            account.AccessToken = tokenResult.accessToken!;
            if (!string.IsNullOrWhiteSpace(tokenResult.refreshToken))
            {
                account.RefreshToken = tokenResult.refreshToken!;
            }
            account.ExpiresAt = DateTimeOffset.Now.AddSeconds(tokenResult.expiresIn);
            s_logger.Info($"微软账号令牌刷新成功: {account.Name}");

            var xblResult = await GetXblToken(account.AccessToken, cancellationToken);
            if (xblResult.exception != null || string.IsNullOrWhiteSpace(xblResult.xblToken))
            {
                s_logger.Warn($"微软账号获取 XBL Token 失败: {account.Name}");
                return (null, xblResult.exception ?? new Exception("Failed to get xbl token"));
            }

            var xstsResult = await GetXstsToken(xblResult.xblToken!, cancellationToken);
            if (xstsResult.exception != null ||
                string.IsNullOrWhiteSpace(xstsResult.xstsToken) ||
                string.IsNullOrWhiteSpace(xstsResult.userHash))
            {
                s_logger.Warn($"微软账号获取 XSTS Token 失败: {account.Name}");
                return (null, xstsResult.exception ?? new Exception("Failed to get xsts token"));
            }

            s_logger.Info($"微软账号已获取 Minecraft 服务令牌: {account.Name}");
            return await GetMinecraftAccessToken(xstsResult.userHash!, xstsResult.xstsToken!, cancellationToken);
        }
        catch (Exception ex)
        {
            s_logger.Warn($"微软账号获取 Minecraft 服务令牌失败: {account.Name}");
            return (null, ex);
        }
    }
    
    async private static Task<(string? xstsToken, string? userHash, Exception? exception)> GetXstsToken(string xblToken, CancellationToken cancellationToken)
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
            var json = JsonUtils.Parse(responseString);
            var token = json.GetString("Token");
            var userHash = json.GetString("DisplayClaims.xui[0].uhs");
            return (token, userHash, null);
        }catch (Exception ex)
        {
            return (null, null, ex);
        }
    }
    
    async private static Task<(string? xblToken, Exception? exception)> GetXblToken(string accessToken, CancellationToken cancellationToken)
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
                var json = JsonUtils.Parse(responseString);
                var token = json.GetString("Token");
                return (token, null);
            }
            catch (Exception ex) when (attempt == 0 && ex.Message.Contains("400"))
            {
                continue;
            }
            catch (Exception ex)
            {
                return (null, ex);
            }
        }
        return (null, new Exception("XBL token acquisition failed after retries"));
    }
    
    async private static Task<(string? accessToken, string? refreshToken, int expiresIn, Exception? exception)> GetTokenByAuthCode(string code, CancellationToken cancellationToken)
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
            var json = JsonUtils.Parse(responseString);
            var accessToken = json.GetString("access_token");
            var refreshToken = json.GetString("refresh_token");
            var expiresIn = json.GetOrDefault("expires_in", 3600);
            return (accessToken, refreshToken, expiresIn, null);
        }catch (Exception ex)
        {
            return (null, null, 0, ex);
        }
    }

    async private static Task<(string? accessToken, string? refreshToken, int expiresIn, Exception? exception)> GetTokenByRefreshToken(string refreshToken, CancellationToken cancellationToken)
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
            var json = JsonUtils.Parse(responseString);
            var accessToken = json.GetString("access_token");
            var newRefreshToken = json.GetString("refresh_token");
            var expiresIn = json.GetOrDefault("expires_in", 3600);
            return (accessToken, newRefreshToken, expiresIn, null);
        }
        catch (Exception ex)
        {
            return (null, null, 0, ex);
        }
    }
    
    

    async private static Task<(string? code, Exception? exception)> GetAuthCode(CancellationToken cancellationToken)
    {
        try
        {
            s_listener = new HttpListener();
            s_listener.Prefixes.Add("http://localhost:40935/");
            s_listener.Start();
            var context = await s_listener.GetContextAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var request = context.Request;
            var response = context.Response;
            var code = request.QueryString["code"];
            if (request.Url?.AbsolutePath == "/success" && !string.IsNullOrEmpty(code))
            {
                response.StatusCode = 200;
            }
            else
            {
                response.StatusCode = 400;
            }
            response.AddHeader("Access-Control-Allow-Origin", "https://blog.huangyu.win");
            if (request.HttpMethod == "OPTIONS")
            {
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept");
            }
            response.Close();
            s_listener.Stop();
            return (code, null);
        }catch (Exception ex)
        {
            return (null, ex);
        }
    }
}

public record OAuthReport(int Step, int TotalStep, string Message);
