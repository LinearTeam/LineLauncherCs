using System.Text.Json;
using LMCCore.Utils;
using Xunit;

namespace LMCCore.Test.Utils;

public class JsonUtilsTests
{
    [Fact]
    public void Parse_WithValidJson_ReturnsJsonUtils()
    {
        // Arrange
        string json = @"{""name"":""test"",""value"":123}";

        // Act
        var result = JsonUtils.Parse(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(json, result.ToString());
    }

    [Fact]
    public void Parse_WithInvalidJson_ReturnsInvalidJsonUtils()
    {
        // Arrange
        string json = "invalid json";

        // Act
        var result = JsonUtils.Parse(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("null", result.ToString());
    }

    [Fact]
    public void GetValueFromJson_WithValidJsonAndPath_ReturnsValue()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test""}}";

        // Act
        var result = JsonUtils.GetValueFromJson(json, "user.name");

        // Assert
        Assert.Equal("test", result);
    }

    [Fact]
    public void GetValueFromJson_WithInvalidJson_ReturnsEmptyString()
    {
        // Arrange
        string json = "invalid json";

        // Act
        var result = JsonUtils.GetValueFromJson(json, "user.name");

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void GetObject_WithValidPath_ReturnsJsonUtils()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test""}}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.GetObject("user");

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GetObject_WithInvalidPath_ReturnsInvalidJsonUtils()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test""}}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.GetObject("nonexistent");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("null", result.ToString());
    }

    [Fact]
    public void GetString_WithValidPath_ReturnsString()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test""}}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.GetString("user.name");

        // Assert
        Assert.Equal("test", result);
    }

    [Fact]
    public void GetString_WithInvalidPath_ReturnsNull()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test""}}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.GetString("user.nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetStringOrDefault_WithValidPath_ReturnsString()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test""}}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.GetStringOrDefault("user.name");

        // Assert
        Assert.Equal("test", result);
    }

    [Fact]
    public void GetStringOrDefault_WithInvalidPath_ReturnsDefault()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test""}}";
        var utils = JsonUtils.Parse(json);
        string defaultValue = "default";

        // Act
        var result = utils.GetStringOrDefault("user.nonexistent", defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public void GetArray_WithValidPath_ReturnsArray()
    {
        // Arrange
        string json = @"{""items"":[1,2,3]}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.GetArray<int>("items");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);
        Assert.Equal(3, result[2]);
    }

    [Fact]
    public void GetArray_WithInvalidPath_ReturnsNull()
    {
        // Arrange
        string json = @"{""items"":[1,2,3]}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.GetArray<int>("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetArrayOrDefault_WithValidPath_ReturnsArray()
    {
        // Arrange
        string json = @"{""items"":[1,2,3]}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.GetArrayOrDefault<int>("items");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void GetArrayOrDefault_WithInvalidPath_ReturnsDefault()
    {
        // Arrange
        string json = @"{""items"":[1,2,3]}";
        var utils = JsonUtils.Parse(json);
        var defaultValue = new List<int> { 4, 5, 6 };

        // Act
        var result = utils.GetArrayOrDefault("nonexistent", defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public void Get_WithValidPath_ReturnsDeserializedObject()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test"",""age"":30}}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.Get<TestUser>("user");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void Get_WithInvalidPath_ReturnsDefault()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test"",""age"":30}}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.Get<TestUser>("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetOrDefault_WithValidPath_ReturnsDeserializedObject()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test"",""age"":30}}";
        var utils = JsonUtils.Parse(json);

        // Act
        var result = utils.GetOrDefault<TestUser>("user");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public void GetOrDefault_WithInvalidPath_ReturnsDefaultValue()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test"",""age"":30}}";
        var utils = JsonUtils.Parse(json);
        var defaultValue = new TestUser { Name = "default", Age = 0 };

        // Act
        var result = utils.GetOrDefault("nonexistent", defaultValue);

        // Assert
        Assert.Equal(defaultValue, result);
    }

    [Fact]
    public void Merge_WithValidJson_MergesSuccessfully()
    {
        // Arrange
        string json1 = @"{""user"":{""name"":""test"",""age"":30}}";
        string json2 = @"{""user"":{""age"":31,""email"":""test@example.com""}}";
        var utils1 = JsonUtils.Parse(json1);
        var utils2 = JsonUtils.Parse(json2);

        // Act
        var result = utils1.Merge(utils2);

        // Assert
        Assert.NotNull(result);
        var mergedUser = result.Get<TestUser>("user");
        Assert.NotNull(mergedUser);
        Assert.Equal("test", mergedUser.Name);
        Assert.Equal(31, mergedUser.Age);
    }

    [Fact]
    public void Merge_WithIgnorePaths_ExcludesSpecifiedPaths()
    {
        // Arrange
        string json1 = @"{""user"":{""name"":""test"",""age"":30}}";
        string json2 = @"{""user"":{""age"":31,""email"":""test@example.com""}}";
        var utils1 = JsonUtils.Parse(json1);
        var utils2 = JsonUtils.Parse(json2);
        var ignorePaths = new[] { "user.age" };

        // Act
        var result = utils1.Merge(utils2, ignorePaths);

        // Assert
        Assert.NotNull(result);
        var mergedUser = result.Get<TestUser>("user");
        Assert.NotNull(mergedUser);
        Assert.Equal("test", mergedUser.Name);
        Assert.Equal(30, mergedUser.Age);
    }

    [Fact]
    public void Merge_WithInvalidUtils_ReturnsValid()
    {
        // Arrange
        string json1 = @"{""user"":{""name"":""test"",""age"":30}}";
        string invalidJson = "invalid";
        var utils1 = JsonUtils.Parse(json1);
        var utils2 = JsonUtils.Parse(invalidJson);

        // Act
        var result = utils1.Merge(utils2);

        // Assert
        Assert.Equal(json1, result.ToString());
    }

    [Fact]
    public void ToJsonElement_WithValidJson_ReturnsElement()
    {
        // Arrange
        string json = @"{""user"":{""name"":""test""}}";
        var utils = JsonUtils.Parse(json);

        // Act
        var element = utils.ToJsonElement();

        // Assert
        Assert.True(element.ValueKind == JsonValueKind.Object);
        Assert.True(element.TryGetProperty("user", out var userProperty));
        Assert.True(userProperty.TryGetProperty("name", out var nameProperty));
        Assert.Equal("test", nameProperty.GetString());
    }

    [Fact]
    public void ToJsonElement_WithInvalidJson_ReturnsDefault()
    {
        // Arrange
        var utils = JsonUtils.Parse("invalid json");

        // Act
        var element = utils.ToJsonElement();

        // Assert
        Assert.Equal(default(JsonElement), element);
    }

    private class TestUser
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string Email { get; set; }
    }
}
