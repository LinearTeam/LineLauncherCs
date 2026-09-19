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

using System.Text.Json.Serialization.Metadata;
using LMCCore.Account;
using LMCCore.Game.Model.LocalVersion.Arguments;
using LMCCore.Game.Model.LocalVersion.Libraries;

namespace LMCCore.Utils;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public class JsonUtils
{
    public readonly static JsonSerializerOptions DefaultSerializerOptions = CreateDefaultSerializerOptions();
    public readonly static JsonSerializerOptions AccountSerializerOptions = CreateAccountSerializerOptions();
    public JsonNode? Node { get; }
    public bool IsValid { get; }

    private JsonUtils(JsonNode? node, bool isValid = true)
    {
        Node = node;
        IsValid = isValid;
    }

    public static JsonUtils Parse(string json)
    {
        return TryParse(json, out var parsed) ? parsed : new JsonUtils(null, false);
    }

    public static bool TryParse(string json, out JsonUtils parsed)
    {
        try
        {
            var node = JsonNode.Parse(json);
            parsed = new JsonUtils(node);
            return true;
        }
        catch
        {
            parsed = new JsonUtils(null, false);
            return false;
        }
    }
    
    /// <summary>
    /// 根据路径从JSON中获取字符串值
    /// </summary>
    /// <param name="json">JSON字符串</param>
    /// <param name="path">点分路径，如 "downloads.artifact.url"</param>
    /// <returns>获取到的字符串值，如果不存在则返回null</returns>
    public static string? GetValueFromJson(string json, string path)
    {
        try
        {
            var node = Parse(json);
            return node.GetString(path);
        }
        catch
        {
            return null;
        }
    }

    public JsonUtils GetObject(string path)
    {
        var result = GetNode(path);
        return result.Node is JsonObject
            ? result
            : new JsonUtils(null, false);
    }

    public string? GetString(string path)
    {
        var node = GetNode(path).Node;
        return node?.GetValueKind() == JsonValueKind.String ? node.ToString() : null;
    }

    public string? GetStringOrDefault(string path, string? defaultValue = null)
    {
        var value = GetString(path);
        return value ?? defaultValue;
    }

    public List<T>? GetArray<T>()
    {
        var node = Node;
        if (node is not JsonArray array) return null;

        try
        {
            return array.Deserialize<List<T>>(DefaultSerializerOptions);
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
        var node = GetNode(path).Node;
        if (node is not JsonArray array) return null;

        try
        {
            return array.Deserialize<List<T>>(DefaultSerializerOptions);
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

    public T? Get<T>()
    {
        return Node.Deserialize<T>(DefaultSerializerOptions) ?? default;
    }

    public static bool TryDeserialize<T>(string json, out T? value, JsonSerializerOptions? options = null)
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(json, options ?? DefaultSerializerOptions);
            return value != null;
        }
        catch
        {
            value = default;
            return false;
        }
    }
    public T? Get<T>(string path)
    {
        var node = GetNode(path).Node;
        return node != null ? node.Deserialize<T>(DefaultSerializerOptions) : default;
    }

    public T? GetOrDefault<T>(string path, T? defaultValue = default)
    {
        var result = Get<T>(path);
        return result != null ? result : defaultValue;
    }

    public bool HasValue(string path)
    {
        return GetNode(path).Node != null;
    }

    public bool Set<T>(string path, T? value)
    {
        if (value is JsonUtils jsonUtils)
        {
            return Set(path, jsonUtils.Node);
        }

        if (value is JsonNode jsonNode)
        {
            return Set(path, jsonNode);
        }

        return Set(path, JsonSerializer.SerializeToNode(value, DefaultSerializerOptions));
    }

    public bool Set(string path, JsonNode? value)
    {
        if (!TryParsePath(path, out var segments) || !IsValid || Node == null)
        {
            return false;
        }

        if (segments.Count == 0)
        {
            return false;
        }

        var parentNode = Node;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var nextSegment = segments[i + 1];
            var resolvedNode = ResolveSegment(parentNode, segments[i], createMissing: true, nextSegment);
            if (resolvedNode == null)
            {
                return false;
            }

            parentNode = resolvedNode;
        }

        return SetSegmentValue(parentNode, segments[^1], value?.DeepClone());
    }

    public bool Delete(string path)
    {
        if (!TryParsePath(path, out var segments) || !IsValid || Node == null)
        {
            return false;
        }

        if (segments.Count == 0)
        {
            return false;
        }

        var parentNode = Node;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            var resolvedNode = ResolveSegment(parentNode, segments[i], createMissing: false);
            if (resolvedNode == null)
            {
                return false;
            }

            parentNode = resolvedNode;
        }

        return DeleteSegmentValue(parentNode, segments[^1]);
    }

    public JsonUtils Clone()
    {
        return new JsonUtils(Node?.DeepClone(), IsValid);
    }

    public JsonUtils Merge(JsonUtils other, IEnumerable<string>? ignorePaths = null)
    {
        if (!IsValid || Node == null) return other;
        if (!other.IsValid || other.Node == null) return this;

        var current = Node.DeepClone();
        var otherClone = other.Node.DeepClone();
        return new JsonUtils(MergeNodes(current, otherClone, ignorePaths ?? Enumerable.Empty<string>(), ""));
    }

    private JsonNode MergeNodes(JsonNode current, JsonNode other, IEnumerable<string> ignorePaths, string currentPath)
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
                        currentObj[property.Key] = MergeNodes(existing, property.Value, ignorePaths, fullPath);
                    }
                }
                else
                {
                    currentObj[property.Key] = property.Value?.DeepClone();
                }
            }

            return currentObj;
        }

        if (current is JsonArray currentArr && other is JsonArray otherArr)
        {
            foreach (var item in otherArr)
            {
                currentArr.Add(item?.DeepClone());
            }

            return currentArr;
        }

        return other.DeepClone();
    }

    public override string ToString() => Node?.ToJsonString() ?? "null";
    public JsonElement ToJsonElement() => Node?.Deserialize<JsonElement>(DefaultSerializerOptions) ?? default;

    private static JsonSerializerOptions CreateDefaultSerializerOptions()
    {
        var options = CreateBaseSerializerOptions();
        options.Converters.Add(new GameArgumentConverter());
        options.Converters.Add(new LibraryInfoConverter());
        return options;
    }

    private static JsonSerializerOptions CreateAccountSerializerOptions()
    {
        var options = CreateBaseSerializerOptions();
        options.Converters.Add(new AccountJsonConverter());
        return options;
    }

    private static JsonSerializerOptions CreateBaseSerializerOptions() => new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private JsonUtils GetNode(string path)
    {
        if (!IsValid || Node == null || !TryParsePath(path, out var segments))
        {
            return new JsonUtils(null, false);
        }

        JsonNode? currentNode = Node;
        foreach (var segment in segments)
        {
            if (currentNode == null)
            {
                break;
            }

            currentNode = ResolveSegment(currentNode, segment, createMissing: false);
        }

        return new JsonUtils(currentNode, currentNode != null);
    }

    private static JsonNode? ResolveSegment(
        JsonNode? node,
        JsonPathSegment segment,
        bool createMissing,
        JsonPathSegment? nextSegment = null)
    {
        if (node == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(segment.PropertyName))
        {
            if (node is not JsonObject obj)
            {
                return null;
            }

            if (!obj.TryGetPropertyValue(segment.PropertyName, out node) || node == null)
            {
                if (!createMissing)
                {
                    return null;
                }

                node = CreateContainerForSegment(segment, nextSegment);
                obj[segment.PropertyName] = node;
            }
        }

        if (segment.Indexes.Length == 0)
        {
            return node;
        }

        for (var i = 0; i < segment.Indexes.Length; i++)
        {
            var index = segment.Indexes[i];
            if (index < 0 || node is not JsonArray array)
            {
                return null;
            }

            if (!createMissing && index >= array.Count)
            {
                return null;
            }

            EnsureArraySize(array, index);

            var element = array[index];
            if (element == null)
            {
                if (!createMissing)
                {
                    return null;
                }

                element = CreateContainerForIndex(segment, i, nextSegment);
                array[index] = element;
            }

            node = element;
        }

        return node;
    }

    private static bool SetSegmentValue(JsonNode parentNode, JsonPathSegment segment, JsonNode? value)
    {
        if (segment.Indexes.Length == 0)
        {
            if (parentNode is not JsonObject parentObject || string.IsNullOrEmpty(segment.PropertyName))
            {
                return false;
            }

            parentObject[segment.PropertyName] = value;
            return true;
        }

        JsonNode? targetNode = parentNode;
        if (!string.IsNullOrEmpty(segment.PropertyName))
        {
            if (parentNode is not JsonObject parentObject)
            {
                return false;
            }

            if (!parentObject.TryGetPropertyValue(segment.PropertyName, out targetNode) || targetNode == null)
            {
                targetNode = new JsonArray();
                parentObject[segment.PropertyName] = targetNode;
            }
        }

        return SetArrayElement(targetNode, segment.Indexes, value);
    }

    private static bool DeleteSegmentValue(JsonNode parentNode, JsonPathSegment segment)
    {
        if (segment.Indexes.Length == 0)
        {
            return parentNode is JsonObject parentObject
                   && !string.IsNullOrEmpty(segment.PropertyName)
                   && parentObject.Remove(segment.PropertyName);
        }

        JsonNode? targetNode = parentNode;
        if (!string.IsNullOrEmpty(segment.PropertyName))
        {
            if (parentNode is not JsonObject parentObject
                || !parentObject.TryGetPropertyValue(segment.PropertyName, out targetNode)
                || targetNode == null)
            {
                return false;
            }
        }

        return RemoveArrayElement(targetNode, segment.Indexes);
    }

    private static bool SetArrayElement(JsonNode? node, IReadOnlyList<int> indexes, JsonNode? value)
    {
        if (node is not JsonArray array || indexes.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < indexes.Count - 1; i++)
        {
            var index = indexes[i];
            if (index < 0)
            {
                return false;
            }

            EnsureArraySize(array, index);

            var element = array[index];
            if (element == null)
            {
                element = new JsonArray();
                array[index] = element;
            }

            if (element is not JsonArray nestedArray)
            {
                return false;
            }

            array = nestedArray;
        }

        var lastIndex = indexes[^1];
        if (lastIndex < 0)
        {
            return false;
        }

        EnsureArraySize(array, lastIndex);
        array[lastIndex] = value;
        return true;
    }

    private static bool RemoveArrayElement(JsonNode? node, IReadOnlyList<int> indexes)
    {
        if (node is not JsonArray array || indexes.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < indexes.Count - 1; i++)
        {
            var index = indexes[i];
            if (index < 0 || index >= array.Count || array[index] is not JsonArray nestedArray)
            {
                return false;
            }

            array = nestedArray;
        }

        var lastIndex = indexes[^1];
        if (lastIndex < 0 || lastIndex >= array.Count)
        {
            return false;
        }

        array.RemoveAt(lastIndex);
        return true;
    }

    private static JsonNode CreateContainerForSegment(JsonPathSegment segment, JsonPathSegment? nextSegment)
    {
        return segment.Indexes.Length > 0
            ? new JsonArray()
            : CreateContainerForNextSegment(nextSegment);
    }

    private static JsonNode CreateContainerForIndex(JsonPathSegment segment, int currentIndexPosition, JsonPathSegment? nextSegment)
    {
        return currentIndexPosition < segment.Indexes.Length - 1
            ? new JsonArray()
            : CreateContainerForNextSegment(nextSegment);
    }

    private static JsonNode CreateContainerForNextSegment(JsonPathSegment? nextSegment)
    {
        if (nextSegment is { PropertyName: "", Indexes.Length: > 0 })
        {
            return new JsonArray();
        }

        return new JsonObject();
    }

    private static void EnsureArraySize(JsonArray array, int index)
    {
        while (array.Count <= index)
        {
            array.Add(null);
        }
    }

    private static bool TryParsePath(string path, out List<JsonPathSegment> segments)
    {
        segments = [];
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                segments.Add(ParsePathSegment(part));
            }

            return segments.Count > 0;
        }
        catch
        {
            segments.Clear();
            return false;
        }
    }

    private static JsonPathSegment ParsePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            throw new FormatException("JSON path segment cannot be empty.");
        }

        var firstBracketIndex = segment.IndexOf('[');
        if (firstBracketIndex < 0)
        {
            return new JsonPathSegment(segment, Array.Empty<int>());
        }

        var propertyName = firstBracketIndex == 0 ? string.Empty : segment[..firstBracketIndex];
        var indexes = new List<int>();
        var position = firstBracketIndex;

        while (position < segment.Length)
        {
            if (segment[position] != '[')
            {
                throw new FormatException("Invalid JSON array path segment.");
            }

            var endBracketIndex = segment.IndexOf(']', position + 1);
            if (endBracketIndex <= position + 1)
            {
                throw new FormatException("Invalid JSON array index.");
            }

            indexes.Add(int.Parse(segment[(position + 1)..endBracketIndex]));
            position = endBracketIndex + 1;
        }

        return new JsonPathSegment(propertyName, indexes);
    }

    private sealed record JsonPathSegment(string PropertyName, int[] Indexes)
    {
        public JsonPathSegment(string propertyName, List<int> indexes) : this(propertyName, indexes.ToArray())
        {
        }
    }
}
