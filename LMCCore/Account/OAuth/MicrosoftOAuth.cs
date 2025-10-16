using System.Net;
using LMC.Basic.Logging;
using LMCCore.Account.Model;
using LMCCore.Utils;

namespace LMCCore.Account.OAuth;

public class MicrosoftOAuth
{
    public string LoginUrl = "https://blog.huangyu.win/line/loginRedirect.html?url=https%3a%2f%2flogin.microsoftonline.com%2fconsumers%2foauth2%2fv2.0%2fauthorize%3fclient_id%3d1cbfda79-fc84-47f9-8110-f924da9841ec%26response_type%3dcode%26redirect_uri%3dhttps%3a%2f%2fblog.huangyu.win%2fline%2floginSuccess.html%3f%26response_mode%3dquery%26scope%3dXboxLive.offline_access";
    
    private static readonly Logger s_logger = new Logger("MSOAuth");
    private static HttpListener? s_listener;
    
    public async static Task<MicrosoftAccount?> StartOAuth(Action<OAuthReport> reportAction)
    {
        int total = 5;
        int i = 1;
        s_logger.Info("开始微软登录");
        reportAction(new OAuthReport(i++, total, "等待用户登录"));
        s_logger.Info($"进度：{i}/{total}");
        var codeResult = await GetAuthCode();
        if(codeResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"获取授权码失败: {codeResult.exception.Message}"));
            s_logger.Error(codeResult.exception, $"获取授权码");
            return null;
        }
        var code = codeResult.code;
        Logger.SensitiveData[code!] = "{OACode}";
        s_logger.Info($"进度：{i}/{total}");
        var tokenResult = await GetTokenByAuthCode(code!);
        if (tokenResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"获取访问令牌失败: {tokenResult.exception.Message}"));
            s_logger.Error(tokenResult.exception, $"获取访问令牌");
            return null;
        }
        reportAction(new OAuthReport(i++, total, "Xbox Live身份验证"));
        s_logger.Info($"进度：{i}/{total}");
        var xblResult = await GetXblToken(tokenResult.accessToken!);
        if (xblResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"获取XBL令牌失败: {xblResult.exception.Message}"));
            s_logger.Error(xblResult.exception, $"获取XBL令牌");
            return null;
        }
        Logger.SensitiveData[xblResult.xblToken!] = "{XBLToken}";
        reportAction(new OAuthReport(i++, total, "XSTS身份验证"));
        var xstsResult = await GetXstsToken(xblResult.xblToken!);
        if (xstsResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"获取XSTS令牌失败: {xstsResult.exception.Message}"));
            s_logger.Error(xstsResult.exception, $"获取XSTS令牌");
            return null;
        }
        Logger.SensitiveData[xstsResult.xstsToken!] = "{XSTSToken}";
        Logger.SensitiveData[xstsResult.userHash!] = "{UserHash}";
        s_logger.Info($"进度：{i}/{total}");
        reportAction(new OAuthReport(i++, total, "Minecraft身份验证"));
        var mcResult = await GetMinecraftAccessToken(xstsResult.userHash!, xstsResult.xstsToken!);
        if (mcResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"获取Minecraft令牌失败: {mcResult.exception.Message}"));
            s_logger.Error(mcResult.exception, $"获取Minecraft令牌");
            return null;
        }
        Logger.SensitiveData[mcResult.accessToken!] = "{MCAccessToken}";
        s_logger.Info($"进度：{i}/{total}");
        reportAction(new OAuthReport(i++, total, "验证Minecraft拥有权"));
        var ownershipResult = await CheckMinecraftOwnership(mcResult.accessToken!);
        if (ownershipResult.exception != null)
        {
            reportAction(new OAuthReport(-1, total, $"验证Minecraft拥有权失败: {ownershipResult.exception.Message}"));
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
            bool haveMc = items.Count > 0;
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
            var uuid = profileJson.GetString("id");
            var name = profileJson.GetString("name");
            return (true, uuid, name, null);
        }catch (Exception ex)
        {
            return (false, null, null, ex);
        }
    }
    
    async private static Task<(string? accessToken, Exception? exception)> GetMinecraftAccessToken(string userHash, string xstsToken)
    {
        try
        {
            var response = await HttpUtils.CreateRequest("https://api.minecraftservices.com/authentication/login_with_xbox")
                .WithJsonContent(new
                {
                    identityToken = $"XBL3.0 x={userHash};{xstsToken}"
                })
                .PostAsync();
            var responseString = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var json = JsonUtils.Parse(responseString);
            var token = json.GetString("access_token");
            return (token, null);
        }catch (Exception ex)
        {
            return (null, ex);
        }
    }
    
    async private static Task<(string? xstsToken, string? userHash, Exception? exception)> GetXstsToken(string xblToken)
    {
        try
        {
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
                .PostAsync();
            var responseString = await response.Content.ReadAsStringAsync();
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
    
    async private static Task<(string? xblToken, Exception? exception)> GetXblToken(string accessToken)
    {
        bool dEq = true;
        retry:
        try
        {
            var response = await HttpUtils.CreateRequest("https://user.auth.xboxlive.com/user/authenticate")
                .WithJsonContent(new
                {
                    Properties = new
                    {
                        AuthMethod = "RPS",
                        SiteName = "user.auth.xboxlive.com",
                        RpsTicket = $"{(dEq ? "d=" : "")}{accessToken}"
                    },
                    RelyingParty = "http://auth.xboxlive.com",
                    TokenType = "JWT"
                })
                .PostAsync();
            var responseString = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
            var json = JsonUtils.Parse(responseString);
            var token = json.GetString("Token");
            // var userHash = json.GetString("DisplayClaims.xui[0].uhs");
            return (token, null);
        }catch (Exception ex)
        {
            if(ex.Message.Contains("400") && dEq)
            {
                dEq = false;
                goto retry;
            }
            return (null, ex);
        }
    }
    
    async private static Task<(string? accessToken, string? refreshToken, int expiresIn, Exception? exception)> GetTokenByAuthCode(string code)
    {
        try
        {
            var response = await HttpUtils.CreateRequest("https://login.microsoftonline.com/consumers/oauth2/v2.0/token")
                .WithFormContent(builder => builder
                    .Add("client_id", "1cbfda79-fc84-47f9-8110-f924da9841ec")
                    .Add("code", code)
                    .Add("grant_type", "authorization_code")
                    .Add("redirect_uri", "https://blog.huangyu.win/line/loginSuccess.html?")
                    .Add("scope", "XboxLive.signin offline_access"))
                .PostAsync();
            var responseString = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
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
    
    

    async private static Task<(string? code, Exception? exception)> GetAuthCode()
    {
        try
        {
            s_listener = new HttpListener();
            s_listener.Prefixes.Add("http://localhost:40935/");
            s_listener.Start();
            var context = await s_listener.GetContextAsync();
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