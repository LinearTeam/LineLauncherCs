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

namespace LMCCore.Game.Download.Installation.OptiFine;

public sealed class OptiFineInstallationRuntimeState
{
    public required string MinecraftVersion { get; init; }

    public required string Patch { get; init; }

    public string Type { get; init; } = "HD_U";

    public required string InstallerDownloadUrl { get; init; }

    public required string CachedInstallerJarPath { get; init; }

    public required string CachedMinecraftJarPath { get; init; }

    public required string InstallerLibraryCoordinate { get; init; }

    public required string InstallerLibraryPath { get; init; }

    public required string LibraryCoordinate { get; init; }

    public required string LibraryPath { get; init; }

    public bool InstallAsStandaloneMod { get; set; }

    public string? StandaloneModFileName { get; set; }

    public string? LaunchWrapperCoordinate { get; set; }

    public string? LaunchWrapperPath { get; set; }

    public bool HasEmbeddedLaunchWrapper { get; set; }

    public bool HasPatcher { get; set; }
}
