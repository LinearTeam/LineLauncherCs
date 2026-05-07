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

using System.Text.Json;
using System.Text.Json.Serialization;
using LMCCore.Game.Model.LocalVersion.Compatibility;

namespace LMCCore.Game.Model.LocalVersion.Arguments;

public interface IGameArgument;

public class StringGameArgument : IGameArgument
{
    public required string Value { get; set; }
}

public class ConditionGameArguments : IGameArgument
{
    public List<CompatibilityRule>? Rules { get; set; }
    public required IConditionArgumentValue Value { get; set; }
}

public interface IConditionArgumentValue;

public class StringConditionArgumentValue : IConditionArgumentValue
{
    public required string Value { get; set; }
}

public class StringArrayConditionArgumentValue : IConditionArgumentValue
{
    public required List<string> Values { get; set; }
}

public class GameArgumentConverter : JsonConverter<IGameArgument>
{
    public override IGameArgument? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var readerCopy = reader; 
        
        switch (readerCopy.TokenType)
        {
            case JsonTokenType.String:
                return new StringGameArgument { Value = reader.GetString()! };

            case JsonTokenType.StartObject:
            {
                using var document = JsonDocument.ParseValue(ref reader);
                var jsonObject = document.RootElement;
            
                if (jsonObject.TryGetProperty("rules", out var rulesProp) &&
                    jsonObject.TryGetProperty("value", out var valueProp))
                {
                    var argument = new ConditionGameArguments
                    {
                        Rules = JsonSerializer.Deserialize<List<CompatibilityRule>>(rulesProp.GetRawText(),
                            options)!,
                        Value = null!,
                    };

                    argument.Value = valueProp.ValueKind switch
                    {
                        JsonValueKind.String => new StringConditionArgumentValue
                        {
                            Value = valueProp.GetString()!
                        },
                        JsonValueKind.Array => new StringArrayConditionArgumentValue
                        {
                            Values = JsonSerializer.Deserialize<List<string>>(valueProp.GetRawText(), options)!
                        },
                        _ => argument.Value
                    };

                    return argument;
                }
                break;
            }

            default:
                return null;
        }
        
        throw new JsonException($"无法解析的参数类型: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, IGameArgument value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (object)value, options);
    }
}