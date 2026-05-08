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

using System.Text.Json.Serialization;

namespace LMCCore.Game.Model.LocalVersion.Compatibility;

public class CompatibilityRule
{
    [JsonPropertyName("action")]
    public required string Action { get; set; }
    [JsonPropertyName("os")]
    public RuleOs? Os { get; set; }
    [JsonPropertyName("features")]
    public Dictionary<string, bool>? Features { get; set; }
}

public class RuleOs
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arch")]
    public string? Arch { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}


public enum RuleAction
{
    [JsonPropertyName("allow")]
    Allow,
    [JsonPropertyName("disallow")]
    Disallow
}
