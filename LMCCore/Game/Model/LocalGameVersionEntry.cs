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
using LMCCore.Game.Model.LocalVersion;

namespace LMCCore.Game.Model;

public class LocalGameVersionEntry
{
    public const string UnknownClientVersionId = "未知版本";

    public required string RootPath { get; init; }

    public required string VersionName { get; init; }

    public required string VersionDirectory { get; init; }

    public string? JarPath { get; init; }

    public string? JsonPath { get; init; }

    public string ClientVersionId { get; init; } = UnknownClientVersionId;

    public LocalVersionInfo? VersionInfo { get; init; }

    public required VersionStatus Status { get; init; }
    
}
