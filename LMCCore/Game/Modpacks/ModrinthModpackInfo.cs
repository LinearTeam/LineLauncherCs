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

namespace LMCCore.Game.Modpacks;

public sealed class ModrinthModpackIndex
{
    [JsonRequired]
    public int FormatVersion { get; init; }
    [JsonRequired]
    public string Game { get; init; } = "";
    [JsonRequired]
    public string Name { get; init; } = "";
    [JsonRequired]
    public string VersionId { get; init; } = "";
    public string? Summary { get; init; }
    [JsonRequired]
    public List<ModrinthModpackFile> Files { get; init; } = [];
    [JsonRequired]
    public Dictionary<string, string> Dependencies { get; init; } = [];
}

public sealed class ModrinthModpackFile
{
    [JsonRequired]
    public string Path { get; init; } = "";
    [JsonRequired]
    public Dictionary<string, string> Hashes { get; init; } = [];
    public ModrinthModpackEnvironment? Env { get; init; }
    [JsonRequired]
    public List<string> Downloads { get; init; } = [];
    [JsonRequired]
    public long FileSize { get; init; }
    [JsonIgnore]
    public bool IsClientOptional => Env?.Client == "optional";
    [JsonIgnore]
    public bool IsClientSupported => Env?.Client != "unsupported";
}

public sealed class ModrinthModpackEnvironment
{
    public string Client { get; init; } = "required";
    public string Server { get; init; } = "required";
}

public sealed record ModrinthModpackInfo(
    string ArchivePath, string ArchiveSha256, ModrinthModpackIndex Index,
    string MinecraftVersion, string? LoaderId, string? LoaderVersion)
{
    public IReadOnlyList<string> OverridePaths { get; init; } = [];
    public IReadOnlyList<ModrinthModpackFile> OptionalFiles =>
        Index.Files.Where(file => file.IsClientOptional).ToArray();
}

public sealed record ModrinthModpackInstallation(
    ModrinthModpackInfo Pack, IReadOnlySet<string> OptionalFilePaths)
{
    public IReadOnlyList<ModrinthModpackFile> GetSelectedFiles() =>
        Pack.Index.Files.Where(file => file.IsClientSupported
            && (!file.IsClientOptional || OptionalFilePaths.Contains(file.Path))).ToArray();
}
