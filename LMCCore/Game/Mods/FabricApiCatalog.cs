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
using LMCCore.Utils;

namespace LMCCore.Game.Mods;

public static class FabricApiCatalog
{
    public static async Task<IReadOnlyList<FabricApiVersion>> GetVersionsAsync(
        string minecraftVersion, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftVersion);
        var games = Uri.EscapeDataString(JsonSerializer.Serialize(new[] { minecraftVersion }));
        var loaders = Uri.EscapeDataString("[\"fabric\"]");
        var url = $"https://api.modrinth.com/v2/project/fabric-api/version?game_versions={games}&loaders={loaders}";
        using var response = await HttpUtils.CreateRequest(url).GetAsync(cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var versions = new List<FabricApiVersion>();
        foreach (var version in document.RootElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var files = version.GetProperty("files").EnumerateArray().ToArray();
            var file = files.FirstOrDefault(file => file.GetProperty("primary").GetBoolean());
            if (file.ValueKind == JsonValueKind.Undefined)
                file = files.FirstOrDefault();
            if (file.ValueKind == JsonValueKind.Undefined)
                continue;

            var fileName = file.GetProperty("filename").GetString();
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName
                || fileName.Contains('\\') || !fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                continue;

            versions.Add(new FabricApiVersion(
                version.GetProperty("version_number").GetString()!, fileName,
                file.GetProperty("url").GetString()!, file.GetProperty("hashes").GetProperty("sha1").GetString()!,
                file.GetProperty("size").GetInt64()));
        }

        return versions;
    }
}
