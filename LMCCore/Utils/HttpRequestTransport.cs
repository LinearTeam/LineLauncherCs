namespace LMCCore.Utils;

internal interface IHttpRequestTransport
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}

internal sealed class HttpClientRequestTransport(HttpClient client) : IHttpRequestTransport
{
    private readonly HttpClient _client = client;

    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _client.SendAsync(request, cancellationToken);
    }
}
