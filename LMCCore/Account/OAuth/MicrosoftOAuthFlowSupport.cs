using System.Net;
using LMC.Basic.Configs;
using LMC.Basic.Logging;
using LMCCore.Account.Model;

namespace LMCCore.Account.OAuth;

internal enum OAuthFlowStep
{
    WaitForCode = 1,
    GetAccessToken = 2,
    XblAuthorize = 3,
    XstsAuthorize = 4,
    MinecraftAuthorize = 5,
    ValidateMinecraft = 6
}

internal sealed class MicrosoftOAuthFlowDependencies
{
    public required Func<CancellationToken, Task<(string? code, Exception? exception)>> GetAuthCodeAsync { get; init; }
    public required Func<string, CancellationToken, Task<OAuthOperationResult<OAuthTokenPayload>>> GetTokenByAuthCodeAsync { get; init; }
    public required Func<string, CancellationToken, Task<OAuthOperationResult<XboxTokenPayload>>> GetXblTokenAsync { get; init; }
    public required Func<string, CancellationToken, Task<OAuthOperationResult<XboxTokenPayload>>> GetXstsTokenAsync { get; init; }
    public required Func<string, string, CancellationToken, Task<OAuthOperationResult<string>>> GetMinecraftAccessTokenAsync { get; init; }
    public required Func<string, Task<OAuthOperationResult<MinecraftOwnershipPayload>>> CheckMinecraftOwnershipAsync { get; init; }
}

internal sealed class OAuthProgressReporter(Action<OAuthReport> reportAction)
{
    public const int TotalSteps = 6;

    private readonly Action<OAuthReport> _reportAction = reportAction;

    public void ReportStep(OAuthFlowStep step)
    {
        _reportAction(new OAuthReport((int)step, TotalSteps, GetMessage(step)));
    }

    public void ReportFailure(string code, Exception exception)
    {
        _reportAction(new OAuthReport(-1, TotalSteps, $"{code}: {exception.Message}"));
    }

    public void ReportCanceled(Exception exception)
    {
        _reportAction(new OAuthReport(-10, TotalSteps, $"CANCEL: {exception.Message}"));
    }

    public void ReportNoMinecraft()
    {
        _reportAction(new OAuthReport(-2, TotalSteps, "该账户不拥有Minecraft"));
    }

    private static string GetMessage(OAuthFlowStep step)
    {
        return step switch
        {
            OAuthFlowStep.WaitForCode => "Messages.AccountManager.OAuth.Steps.WaitForCode.Message",
            OAuthFlowStep.GetAccessToken => "Messages.AccountManager.OAuth.Steps.GetAccessToken.Message",
            OAuthFlowStep.XblAuthorize => "Messages.AccountManager.OAuth.Steps.XBLAuthorize.Message",
            OAuthFlowStep.XstsAuthorize => "Messages.AccountManager.OAuth.Steps.XSTSAuthorize.Message",
            OAuthFlowStep.MinecraftAuthorize => "Messages.AccountManager.OAuth.Steps.MinecraftAuthorize.Message",
            _ => "Messages.AccountManager.OAuth.Steps.ValidateMinecraft.Message"
        };
    }
}

internal sealed class MicrosoftOAuthFlowCoordinator(
    Logger logger,
    OAuthProgressReporter progressReporter,
    MicrosoftOAuthFlowDependencies dependencies)
{
    private readonly Logger _logger = logger;
    private readonly OAuthProgressReporter _progressReporter = progressReporter;
    private readonly MicrosoftOAuthFlowDependencies _dependencies = dependencies;

    public async Task<MicrosoftAccount?> StartAsync(CancellationToken cancellationToken)
    {
        _progressReporter.ReportStep(OAuthFlowStep.WaitForCode);
        LogStep(OAuthFlowStep.WaitForCode);
        var codeResult = await _dependencies.GetAuthCodeAsync(cancellationToken);
        if (codeResult.exception != null)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _progressReporter.ReportCanceled(codeResult.exception);
                return null;
            }

            _progressReporter.ReportFailure("WAIT_FOR_CODE", codeResult.exception);
            _logger.Error(codeResult.exception, "获取授权码");
            return null;
        }

        var code = codeResult.code;
        if (string.IsNullOrWhiteSpace(code))
        {
            var exception = new InvalidOperationException("Empty auth code");
            _progressReporter.ReportFailure("WAIT_FOR_CODE", exception);
            _logger.Error(exception, "获取授权码");
            return null;
        }

        SecretsManager.SensitiveData[code] = "{OACode}";

        _progressReporter.ReportStep(OAuthFlowStep.GetAccessToken);
        LogStep(OAuthFlowStep.GetAccessToken);
        var tokenResult = await _dependencies.GetTokenByAuthCodeAsync(code, cancellationToken);
        if (!tokenResult.IsSuccess || !IsValidTokenPayload(tokenResult.Value))
        {
            var exception = tokenResult.Exception ?? new InvalidOperationException("Invalid access token response");
            _progressReporter.ReportFailure("GET_ACCESS_TOKEN", exception);
            _logger.Error(exception, "获取访问令牌");
            return null;
        }

        _progressReporter.ReportStep(OAuthFlowStep.XblAuthorize);
        LogStep(OAuthFlowStep.XblAuthorize);
        var xblResult = await _dependencies.GetXblTokenAsync(tokenResult.Value!.AccessToken!, cancellationToken);
        if (!xblResult.IsSuccess || string.IsNullOrWhiteSpace(xblResult.Value?.Token))
        {
            var exception = xblResult.Exception ?? new InvalidOperationException("Invalid XBL token response");
            _progressReporter.ReportFailure("XBL_AUTHORIZE", exception);
            _logger.Error(exception, "获取XBL令牌");
            return null;
        }

        SecretsManager.SensitiveData[xblResult.Value.Token!] = "{XBLToken}";

        _progressReporter.ReportStep(OAuthFlowStep.XstsAuthorize);
        LogStep(OAuthFlowStep.XstsAuthorize);
        var xstsResult = await _dependencies.GetXstsTokenAsync(xblResult.Value.Token!, cancellationToken);
        if (!xstsResult.IsSuccess ||
            string.IsNullOrWhiteSpace(xstsResult.Value?.Token) ||
            string.IsNullOrWhiteSpace(xstsResult.Value?.UserHash))
        {
            var exception = xstsResult.Exception ?? new InvalidOperationException("Invalid XSTS token response");
            _progressReporter.ReportFailure("XSTS_AUTHORIZE", exception);
            _logger.Error(exception, "获取XSTS令牌");
            return null;
        }

        SecretsManager.SensitiveData[xstsResult.Value.Token!] = "{XSTSToken}";
        SecretsManager.SensitiveData[xstsResult.Value.UserHash!] = "{UserHash}";

        _progressReporter.ReportStep(OAuthFlowStep.MinecraftAuthorize);
        LogStep(OAuthFlowStep.MinecraftAuthorize);
        var minecraftTokenResult = await _dependencies.GetMinecraftAccessTokenAsync(
            xstsResult.Value.UserHash!,
            xstsResult.Value.Token!,
            cancellationToken);
        if (!minecraftTokenResult.IsSuccess || string.IsNullOrWhiteSpace(minecraftTokenResult.Value))
        {
            var exception = minecraftTokenResult.Exception ?? new InvalidOperationException("Invalid minecraft token response");
            _progressReporter.ReportFailure("MINECRAFT_AUTHORIZE", exception);
            _logger.Error(exception, "获取Minecraft令牌");
            return null;
        }

        SecretsManager.SensitiveData[minecraftTokenResult.Value!] = "{MCAccessToken}";

        _progressReporter.ReportStep(OAuthFlowStep.ValidateMinecraft);
        LogStep(OAuthFlowStep.ValidateMinecraft);
        var ownershipResult = await _dependencies.CheckMinecraftOwnershipAsync(minecraftTokenResult.Value!);
        if (!ownershipResult.IsSuccess || ownershipResult.Value == null)
        {
            var exception = ownershipResult.Exception ?? new InvalidOperationException("Invalid minecraft ownership response");
            _progressReporter.ReportFailure("VALIDATE_MINECRAFT", exception);
            _logger.Error(exception, "验证Minecraft拥有权");
            return null;
        }

        if (!ownershipResult.Value.HasMinecraft)
        {
            _progressReporter.ReportNoMinecraft();
            _logger.Info("该账户不拥有Minecraft");
            return null;
        }

        return new MicrosoftAccount
        {
            AccessToken = tokenResult.Value.AccessToken!,
            RefreshToken = tokenResult.Value.RefreshToken!,
            ExpiresAt = DateTimeOffset.Now.AddSeconds(tokenResult.Value.ExpiresIn),
            Type = AccountType.Microsoft,
            Name = ownershipResult.Value.Name!,
            Uuid = ownershipResult.Value.Uuid!
        };
    }

    private void LogStep(OAuthFlowStep step)
    {
        _logger.Info($"进度：{(int)step}/{OAuthProgressReporter.TotalSteps}");
    }

    private static bool IsValidTokenPayload(OAuthTokenPayload? payload)
    {
        return payload != null &&
               !string.IsNullOrWhiteSpace(payload.AccessToken) &&
               !string.IsNullOrWhiteSpace(payload.RefreshToken);
    }
}

internal static class OAuthCallbackListener
{
    public const string LocalCallbackPrefix = "http://localhost:40935/";

    public static HttpListener Create()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add(LocalCallbackPrefix);
        listener.Start();
        return listener;
    }

    public static async Task<(string? code, Exception? exception)> WaitForCodeAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            var request = context.Request;
            var response = context.Response;
            var code = request.QueryString["code"];
            var isSuccess = request.Url?.AbsolutePath == "/success" && !string.IsNullOrEmpty(code);

            response.StatusCode = isSuccess ? 200 : 400;
            response.AddHeader("Access-Control-Allow-Origin", "https://blog.huangyu.win");
            if (request.HttpMethod == "OPTIONS")
            {
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept");
            }

            response.Close();
            return isSuccess
                ? (code, null)
                : (null, new InvalidOperationException("OAuth callback did not contain a valid code"));
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }
}
