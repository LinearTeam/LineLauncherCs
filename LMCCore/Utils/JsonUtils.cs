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

namespace LMCCore.Utils;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public class JsonUtils
{
    public JsonNode? Node { get; }
    public bool IsValid { get; }
    public static JsonSerializerOptions DefaultSerializeOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new AccountJsonConverter(), new GameArgumentConverter() }
    };
    private JsonUtils(JsonNode? node, bool isValid = true)
    {
        Node = node;
        IsValid = isValid;
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
        var node = GetNode(path).Node;
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

    public T? Get<T>()
    {
        return Node.Deserialize<T>(DefaultSerializeOptions) ?? default;
    }
    public T? Get<T>(string path)
    {
        var node = GetNode(path).Node;
        return node != null ? node.Deserialize<T>(DefaultSerializeOptions) : default;
    }

    public T? GetOrDefault<T>(string path, T? defaultValue = default)
    {
        var result = Get<T>(path);
        return result != null ? result : defaultValue;
    }

    public JsonUtils Merge(JsonUtils other, IEnumerable<string>? ignorePaths = null)
    {
        if (!IsValid || Node == null) return other;
        if (!other.IsValid || other.Node == null) return this;

        var current = Node.DeepClone();
        var otherClone = other.Node.DeepClone();
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

    public override string ToString() => Node?.ToJsonString() ?? "null";
    public JsonElement ToJsonElement() => Node?.Deserialize<JsonElement>(DefaultSerializeOptions) ?? default;

    private JsonUtils GetNode(string path)
    {
        if (!IsValid || Node == null) return new JsonUtils(null, false);

        JsonNode? currentNode = Node;
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