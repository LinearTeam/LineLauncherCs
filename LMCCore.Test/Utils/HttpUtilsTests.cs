using System.Net;
using System.Text;
using LMCCore.Utils;
using Xunit;

namespace LMCCore.Test.Utils;

public class HttpUtilsTests
{
    [Fact]
    public void CreateRequest_WithValidUrl_ReturnsBuilder()
    {
        // Arrange
        string url = "https://example.com";

        // Act
        var builder = HttpUtils.CreateRequest(url);

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void CreateRequest_WithInvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        string url = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => HttpUtils.CreateRequest(url));
    }

    [Fact]
    public void HttpRequestBuilder_WithRetryDelay_ValidatesParameter()
    {
        // Arrange
        var builder = HttpUtils.CreateRequest("https://example.com");

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithRetryDelay(-1));
    }

    [Fact]
    public void HttpRequestBuilder_WithRetry_ValidatesParameter()
    {
        // Arrange
        var builder = HttpUtils.CreateRequest("https://example.com");

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.WithRetry(0));
    }

    [Fact]
    public void HttpRequestBuilder_WithMethod_ValidatesParameter()
    {
        // Arrange
        var builder = HttpUtils.CreateRequest("https://example.com");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithMethod(null));
    }

    [Fact]
    public void HttpRequestBuilder_WithHeader_ValidatesParameter()
    {
        // Arrange
        var builder = HttpUtils.CreateRequest("https://example.com");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.WithHeader("", "value"));
        Assert.Throws<ArgumentException>(() => builder.WithHeader(null, "value"));
    }

    [Fact]
    public void HttpRequestBuilder_WithContent_ValidatesParameter()
    {
        // Arrange
        var builder = HttpUtils.CreateRequest("https://example.com");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithContent(null));
    }

    [Fact]
    public void HttpRequestBuilder_WithJsonContent_ValidatesParameter()
    {
        // Arrange
        var builder = HttpUtils.CreateRequest("https://example.com");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithJsonContent(null));
    }

    [Fact]
    public void HttpRequestBuilder_WithTextContent_ValidatesParameter()
    {
        // Arrange
        var builder = HttpUtils.CreateRequest("https://example.com");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithTextContent(null));
    }

    [Fact]
    public void HttpRequestBuilder_WithBinaryContent_ValidatesParameter()
    {
        // Arrange
        var builder = HttpUtils.CreateRequest("https://example.com");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.WithBinaryContent(null));
    }

    [Fact]
    public void ContentBuilder_Json_ValidatesParameter()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => HttpUtils.ContentBuilder.Json(null));
    }

    [Fact]
    public void ContentBuilder_Text_ValidatesParameter()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => HttpUtils.ContentBuilder.Text(null));
    }

    [Fact]
    public void ContentBuilder_Binary_ValidatesParameter()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => HttpUtils.ContentBuilder.Binary(null));
    }

    [Fact]
    public void FormContentBuilder_Add_ValidatesParameter()
    {
        // Arrange
        var builder = new HttpUtils.FormContentBuilder();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => builder.Add("", "value"));
        Assert.Throws<ArgumentException>(() => builder.Add(null, "value"));
    }

    [Fact]
    public void FormContentBuilder_Add_AddsKeyValuePair()
    {
        // Arrange
        var builder = new HttpUtils.FormContentBuilder();
        string key = "testKey";
        string value = "testValue";

        // Act
        builder.Add(key, value);
        var content = builder.Build();

        // Assert
        Assert.NotNull(content);
        Assert.True(content.Headers.ContentType.MediaType.Equals("application/x-www-form-urlencoded"));
    }

    [Fact]
    public void FormContentBuilder_AddWithEnumerable_AddsMultiplePairs()
    {
        // Arrange
        var builder = new HttpUtils.FormContentBuilder();
        var pairs = new List<KeyValuePair<string, string>>
        {
            new("key1", "value1"),
            new("key2", "value2")
        };

        // Act
        builder.Add(pairs);
        var content = builder.Build();

        // Assert
        Assert.NotNull(content);
        Assert.True(content.Headers.ContentType.MediaType.Equals("application/x-www-form-urlencoded"));
    }
}
