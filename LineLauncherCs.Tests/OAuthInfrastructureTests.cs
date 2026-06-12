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
using System.Text;
using System.Linq;
using LMC;
using LMC.Basic.Configs;
using LMCCore.Account;
using LMCCore.Account.Model;
using LMCCore.Account.OAuth;
using LMCCore.Utils;

namespace LineLauncherCs.Tests;

public class OAuthInfrastructureTests : IDisposable
{
    public OAuthInfrastructureTests()
    {
        Current.Config = new AppConfig();
    }

    [Fact]
    public async Task HttpUtils_RetriesWithFreshRequestContent()
    {
        var payloads = new List<string>();
        var sendCount = 0;
        HttpUtils.Transport = new DelegateHttpRequestTransport(async (request, cancellationToken) =>
        {
            payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            sendCount++;
            if (sendCount == 1)
            {
                throw new HttpRequestException("boom");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });

        using var response = await HttpUtils.CreateRequest("https://example.com/test")
            .WithJsonContent(new { value = 42 })
            .WithRetry(2)
            .PostAsync();

        Assert.Equal(2, sendCount);
        Assert.Equal(2, payloads.Count);
        Assert.All(payloads, payload => Assert.Contains("\"value\": 42", payload));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void JsonUtils_Merge_ReplacesScalarValuesAndAddsProperties()
    {
        var current = JsonUtils.Parse("""{"launcher":{"java":"17","memory":"4G"}}""");
        var other = JsonUtils.Parse("""{"launcher":{"memory":"8G","windowed":true}}""");

        var merged = current.Merge(other);

        Assert.Equal("17", merged.GetString("launcher.java"));
        Assert.Equal("8G", merged.GetString("launcher.memory"));
        Assert.True(merged.GetOrDefault("launcher.windowed", false));
    }

    [Fact]
    public async Task CheckMinecraftOwnership_ReturnsFalseWithoutCallingProfileWhenNoEntitlements()
    {
        var profileCalls = 0;
        HttpUtils.Transport = new DelegateHttpRequestTransport((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("mcstore", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(JsonResponse("""{"items":[]}"""));
            }

            profileCalls++;
            return Task.FromResult(JsonResponse("""{"id":"12345678123456781234567812345678","name":"Steve"}"""));
        });

        var result = await MicrosoftOAuth.CheckMinecraftOwnership("token");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.False(result.Value!.HasMinecraft);
        Assert.Equal(0, profileCalls);
    }

    [Fact]
    public async Task GetActiveSkinUrlAsync_FallsBackToFirstSkinWhenNoActiveSkin()
    {
        HttpUtils.Transport = new DelegateHttpRequestTransport((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            return Task.FromResult(url switch
            {
                var value when value.Contains("user.auth.xboxlive.com", StringComparison.OrdinalIgnoreCase) =>
                    JsonResponse("""{"Token":"xbl-token"}"""),
                var value when value.Contains("xsts.auth.xboxlive.com", StringComparison.OrdinalIgnoreCase) =>
                    JsonResponse("""{"Token":"xsts-token","DisplayClaims":{"xui":[{"uhs":"user-hash"}]}}"""),
                var value when value.Contains("login_with_xbox", StringComparison.OrdinalIgnoreCase) =>
                    JsonResponse("""{"access_token":"mc-token"}"""),
                var value when value.Contains("/minecraft/profile", StringComparison.OrdinalIgnoreCase) =>
                    JsonResponse("""{"skins":[{"state":"INACTIVE","url":"https://skins.example/first.png"},{"state":"INACTIVE","url":"https://skins.example/second.png"}]}"""),
                _ => throw new InvalidOperationException($"Unexpected request: {url}")
            });
        });

        var account = new MicrosoftAccount
        {
            Name = "Alex",
            AccessToken = "still-valid",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        var result = await MicrosoftOAuth.GetActiveSkinUrlAsync(account);

        Assert.Null(result.exception);
        Assert.Equal("https://skins.example/first.png", result.skinUrl);
    }

    [Fact]
    public async Task OAuthFlowCoordinator_ReportsSuccessfulStepsInOrder()
    {
        var reports = new List<OAuthReport>();
        var coordinator = new MicrosoftOAuthFlowCoordinator(
            new LMC.Basic.Logging.Logger("OAuthTest"),
            new OAuthProgressReporter(reports.Add),
            new MicrosoftOAuthFlowDependencies
            {
                GetAuthCodeAsync = _ => Task.FromResult<(string?, Exception?)>(("code-1", null)),
                GetTokenByAuthCodeAsync = (_, _) => Task.FromResult(OAuthOperationResult<OAuthTokenPayload>.Success(new OAuthTokenPayload("access", "refresh", 3600))),
                GetXblTokenAsync = (_, _) => Task.FromResult(OAuthOperationResult<XboxTokenPayload>.Success(new XboxTokenPayload("xbl"))),
                GetXstsTokenAsync = (_, _) => Task.FromResult(OAuthOperationResult<XboxTokenPayload>.Success(new XboxTokenPayload("xsts", "user-hash"))),
                GetMinecraftAccessTokenAsync = (_, _, _) => Task.FromResult(OAuthOperationResult<string>.Success("mc-token")),
                CheckMinecraftOwnershipAsync = _ => Task.FromResult(OAuthOperationResult<MinecraftOwnershipPayload>.Success(
                    new MinecraftOwnershipPayload(true, "12345678-1234-1234-1234-123456789012", "Steve")))
            });

        var account = await coordinator.StartAsync(CancellationToken.None);

        Assert.NotNull(account);
        Assert.Equal([1, 2, 3, 4, 5, 6], reports.Select(report => report.Step));
        Assert.Equal("Steve", account!.Name);
        Assert.Equal(AccountType.Microsoft, account.Type);
    }

    [Fact]
    public async Task OAuthFlowCoordinator_MapsCancellationToCancelReport()
    {
        var reports = new List<OAuthReport>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var coordinator = new MicrosoftOAuthFlowCoordinator(
            new LMC.Basic.Logging.Logger("OAuthTest"),
            new OAuthProgressReporter(reports.Add),
            new MicrosoftOAuthFlowDependencies
            {
                GetAuthCodeAsync = token => Task.FromResult<(string?, Exception?)>((null, new OperationCanceledException(token))),
                GetTokenByAuthCodeAsync = (_, _) => throw new NotSupportedException(),
                GetXblTokenAsync = (_, _) => throw new NotSupportedException(),
                GetXstsTokenAsync = (_, _) => throw new NotSupportedException(),
                GetMinecraftAccessTokenAsync = (_, _, _) => throw new NotSupportedException(),
                CheckMinecraftOwnershipAsync = _ => throw new NotSupportedException()
            });

        var account = await coordinator.StartAsync(cts.Token);

        Assert.Null(account);
        Assert.Equal(-10, reports.Last().Step);
        Assert.Contains("CANCEL", reports.Last().Message);
    }

    [Fact]
    public async Task OAuthFlowCoordinator_MapsMinecraftAuthorizationFailure()
    {
        var reports = new List<OAuthReport>();
        var coordinator = new MicrosoftOAuthFlowCoordinator(
            new LMC.Basic.Logging.Logger("OAuthTest"),
            new OAuthProgressReporter(reports.Add),
            new MicrosoftOAuthFlowDependencies
            {
                GetAuthCodeAsync = _ => Task.FromResult<(string?, Exception?)>(("code-1", null)),
                GetTokenByAuthCodeAsync = (_, _) => Task.FromResult(OAuthOperationResult<OAuthTokenPayload>.Success(new OAuthTokenPayload("access", "refresh", 3600))),
                GetXblTokenAsync = (_, _) => Task.FromResult(OAuthOperationResult<XboxTokenPayload>.Success(new XboxTokenPayload("xbl"))),
                GetXstsTokenAsync = (_, _) => Task.FromResult(OAuthOperationResult<XboxTokenPayload>.Success(new XboxTokenPayload("xsts", "user-hash"))),
                GetMinecraftAccessTokenAsync = (_, _, _) => Task.FromResult(OAuthOperationResult<string>.Failure(new InvalidOperationException("mc failed"))),
                CheckMinecraftOwnershipAsync = _ => throw new NotSupportedException()
            });

        var account = await coordinator.StartAsync(CancellationToken.None);

        Assert.Null(account);
        Assert.Equal(-1, reports.Last().Step);
        Assert.Contains("MINECRAFT_AUTHORIZE", reports.Last().Message);
    }

    public void Dispose()
    {
        HttpUtils.ResetTransportForTesting();
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class DelegateHttpRequestTransport(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : IHttpRequestTransport
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync = sendAsync;

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _sendAsync(request, cancellationToken);
        }
    }
}
