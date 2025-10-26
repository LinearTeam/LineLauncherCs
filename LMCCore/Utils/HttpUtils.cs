using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using LMC;
using System.Text;

namespace LMCCore.Utils;

public sealed class HttpUtils
{
    private static readonly HttpClient s_httpClient = new HttpClient();

    static HttpUtils()
    {
        s_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"LMC/C{Current.Version}-{Current.BuildNumber}-{Current.VersionType} Mozilla/5.0 ({RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture} {RuntimeInformation.RuntimeIdentifier}) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36 Edg/141.0.0.0");
    }

    public static HttpRequestBuilder CreateRequest(string url) => new HttpRequestBuilder(url);

    public sealed class HttpRequestBuilder
    {
        private readonly string _url;
        private HttpMethod _method = HttpMethod.Get;
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        private HttpContent? _content;
        private TimeSpan? _timeout;
        private int _retry = 1;
        private int _retryDelay = 1000; // milliseconds
        internal HttpRequestBuilder(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL cannot be null or empty", nameof(url));
            
            _url = url;
        }

        public HttpRequestBuilder WithRetryDelay(int milliseconds)
        {
            if (milliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Retry delay must be non-negative");
            
            _retryDelay = milliseconds;
            return this;
        }
        
        public HttpRequestBuilder WithRetry(int retry)
        {
            if (retry < 1)
                throw new ArgumentOutOfRangeException(nameof(retry), "Retry count must be at least 1");
            
            _retry = retry;
            return this;
        }
        
        public HttpRequestBuilder WithMethod(HttpMethod method)
        {
            _method = method ?? throw new ArgumentNullException(nameof(method));
            return this;
        }

        public HttpRequestBuilder WithHeader(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Header name cannot be null or empty", nameof(name));
            
            _headers[name] = value;
            return this;
        }

        public HttpRequestBuilder WithContent(HttpContent content)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            return this;
        }

        public HttpRequestBuilder WithJsonContent(object data, bool setContentType = true, JsonSerializerOptions? options = null)
        {
            if (setContentType)
            {
                 WithHeader("Content-Type", "application/json");
            }
            return WithContent(ContentBuilder.Json(data, options ?? JsonUtils.DefaultSerializeOptions));
        }

        public HttpRequestBuilder WithFormContent(Action<FormContentBuilder> configure, bool setContentType = true)
        {
            if (setContentType)
            {
                WithHeader("Content-Type", "application/x-www-form-urlencoded");
            }
            var builder = new FormContentBuilder();
            configure(builder);
            return WithContent(builder.Build());
        }

        public HttpRequestBuilder WithTextContent(string text, string mediaType = "text/plain", bool setContentType = true)
        {
            if (setContentType)
            {
                WithHeader("Content-Type", mediaType);
            }
            return WithContent(ContentBuilder.Text(text, mediaType));
        }

        public HttpRequestBuilder WithBinaryContent(byte[] data, string mediaType = "application/octet-stream")
        {
            return WithContent(ContentBuilder.Binary(data, mediaType));
        }

        public HttpRequestBuilder WithTimeout(TimeSpan timeout)
        {
            _timeout = timeout;
            return this;
        }

        public async Task<HttpResponseMessage> SendAsync(CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(_method, _url);
            request.Content = _content;
            foreach (var header in _headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            for(int i = 0; i < _retry; i++)
            {
                try
                {
                    if (!_timeout.HasValue) continue;
                    using var cts = new CancellationTokenSource(_timeout.Value);
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                    try
                    {
                        return await s_httpClient.SendAsync(request, linkedCts.Token);
                    }
                    catch (TaskCanceledException ex) when (cts.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Request timed out after {_timeout.Value.TotalSeconds} seconds", ex);
                    }
                    catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }
                }
                catch (Exception)
                {
                    if(i == _retry - 1) throw;
                    if(_retryDelay > 0) 
                    {
                        try
                        {
                            await Task.Delay(_retryDelay, cancellationToken);
                        }
                        catch (TaskCanceledException)
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }
                    }
                }
            }
            
            try
            {
                return await s_httpClient.SendAsync(request, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        public Task<HttpResponseMessage> GetAsync(CancellationToken cancellationToken = default) => WithMethod(HttpMethod.Get).SendAsync(cancellationToken);

        public Task<HttpResponseMessage> PostAsync(CancellationToken cancellationToken = default) => WithMethod(HttpMethod.Post).SendAsync(cancellationToken);
    }

    public static class ContentBuilder
    {
        public static HttpContent? Empty => null;

        public static HttpContent Json(object data, JsonSerializerOptions? options = null, string mediaType = "application/json")
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return new StringContent(
                JsonSerializer.Serialize(data, options),
                Encoding.UTF8,
                mediaType
            );
        }

        public static HttpContent Text(string text, string mediaType = "text/plain")
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return new StringContent(text, Encoding.UTF8, mediaType);
        }

        public static HttpContent Binary(byte[] data, string mediaType = "application/octet-stream")
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var content = new ByteArrayContent(data);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
            return content;
        }
    }

    public sealed class FormContentBuilder
    {
        private readonly Dictionary<string, string> _formData = new Dictionary<string, string>();

        public FormContentBuilder Add(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            
            _formData[key] = value;
            return this;
        }

        public FormContentBuilder Add(IEnumerable<KeyValuePair<string, string>> values)
        {
            foreach (var kvp in values)
            {
                Add(kvp.Key, kvp.Value);
            }
            return this;
        }

        public HttpContent Build()
        {
            return new FormUrlEncodedContent(_formData);
        }
    }
}