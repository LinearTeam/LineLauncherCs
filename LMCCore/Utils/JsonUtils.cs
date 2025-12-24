using System.Text.Json.Serialization.Metadata;
using LMCCore.Account;

namespace LMCCore.Utils;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public class JsonUtils
{
    private readonly JsonNode? _node;
    private readonly bool _isValid;
    public static JsonSerializerOptions DefaultSerializeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new AccountJsonConverter() }
    };
    private JsonUtils(JsonNode? node, bool isValid = true)
    {
        _node = node;
        _isValid = isValid;
    }

    public static JsonUtils Parse(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return new JsonUtils(node);
        }
        catch
        {
            return new JsonUtils(null, false);
        }
    }
    
    public static string? GetValueFromJson(string json, string path)
    {
        try
        {
            return Parse(json)
                .GetStringOrDefault(path, "");
        }
        catch
        {
            return "";
        }
    }

    public JsonUtils GetObject(string path)
    {
        var result = GetNode(path);
        return result._node is JsonObject
            ? result
            : new JsonUtils(null, false);
    }

    public string? GetString(string path)
    {
        var node = GetNode(path)._node;
        return node?.GetValueKind() == JsonValueKind.String ? node.ToString() : null;
    }

    public string? GetStringOrDefault(string path, string? defaultValue = null)
    {
        var value = GetString(path);
        return value ?? defaultValue;
    }

    public List<T>? GetArray<T>()
    {
        var node = this._node;
        if (node is not JsonArray array) return null;

        try
        {
            return array.Deserialize<List<T>>(DefaultSerializeOptions);
        }
        catch
        {
            return null;
        }
    }

    public List<T>? GetArrayOrDefault<T>(List<T>? defaultValue = null)
    {
        var result = GetArray<T>();
        return result ?? defaultValue;
    }

    
    public List<T>? GetArray<T>(string path)
    {
        var node = GetNode(path)._node;
        if (node is not JsonArray array) return null;

        try
        {
            return array.Deserialize<List<T>>(DefaultSerializeOptions);
        }
        catch
        {
            return null;
        }
    }

    public List<T>? GetArrayOrDefault<T>(string path, List<T>? defaultValue = null)
    {
        var result = GetArray<T>(path);
        return result ?? defaultValue;
    }

    public T? Get<T>(string path)
    {
        var node = GetNode(path)._node;
        return node != null ? node.Deserialize<T>(DefaultSerializeOptions) : default;
    }

    public T? GetOrDefault<T>(string path, T? defaultValue = default)
    {
        var result = Get<T>(path);
        return result != null ? result : defaultValue;
    }

    public JsonUtils Merge(JsonUtils other, IEnumerable<string>? ignorePaths = null)
    {
        if (!_isValid || _node == null) return other;
        if (!other._isValid || other._node == null) return this;

        var current = _node.DeepClone();
        var otherClone = other._node.DeepClone();
        MergeNodes(current, otherClone, ignorePaths ?? Enumerable.Empty<string>(), "");
        return new JsonUtils(current);
    }

    private void MergeNodes(JsonNode current, JsonNode other, IEnumerable<string> ignorePaths, string currentPath)
    {
        if (current is JsonObject currentObj && other is JsonObject otherObj)
        {
            foreach (var property in otherObj)
            {
                var fullPath = string.IsNullOrEmpty(currentPath) 
                    ? property.Key 
                    : $"{currentPath}.{property.Key}";
                
                if (ignorePaths.Any(p => fullPath.StartsWith(p))) continue;

                if (currentObj.TryGetPropertyValue(property.Key, out var existing))
                {
                    if (existing != null && property.Value != null)
                    {
                        MergeNodes(existing, property.Value, ignorePaths, fullPath);
                    }
                }
                else
                {
                    currentObj[property.Key] = property.Value?.DeepClone();
                }
            }
        }
        else if (current is JsonArray currentArr && other is JsonArray otherArr)
        {
            foreach (var item in otherArr)
            {
                currentArr.Add(item?.DeepClone());
            }
        }
        else
        {
            current = other.DeepClone();
        }
    }

    public override string ToString() => _node?.ToJsonString() ?? "null";
    public JsonElement ToJsonElement() => _node?.Deserialize<JsonElement>(DefaultSerializeOptions) ?? default;

    private JsonUtils GetNode(string path)
    {
        if (!_isValid || _node == null) return new JsonUtils(null, false);

        JsonNode? currentNode = _node;
        var segments = path.Split('.');

        foreach (var segment in segments)
        {
            if (currentNode == null) break;

            if (segment.Contains('['))
            {
                var parts = segment.Split('[');
                var prop = parts[0];
                var indexes = parts.Skip(1).Select(p => int.Parse(p.TrimEnd(']'))).ToArray();

                currentNode = GetArrayElement(currentNode, prop, indexes);
            }
            else
            {
                currentNode = currentNode[segment];
            }
        }

        return new JsonUtils(currentNode, currentNode != null);
    }

    private JsonNode? GetArrayElement(JsonNode? node, string prop, int[] indexes)
    {
        if (node is JsonObject obj && obj.TryGetPropertyValue(prop, out var value))
        {
            node = value;
        }

        foreach (var index in indexes)
        {
            if (node is JsonArray array && index < array.Count)
            {
                node = array[index];
            }
            else
            {
                return null;
            }
        }
        return node;
    }
}