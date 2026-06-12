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

namespace LMCCore.Game.Download.Installation.Caching;

public sealed class GameInstallationRuntimeState
{
    public string? VersionJson { get; set; }

    public LocalVersionInfo? VersionInfo { get; set; }

    public required string CacheDirectory { get; init; }

    public required string CachedVersionJsonPath { get; init; }

    public required string CachedClientJarPath { get; init; }
}
