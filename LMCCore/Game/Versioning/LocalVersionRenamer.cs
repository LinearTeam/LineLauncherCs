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

using System.Text.Json.Nodes;
using LMC;
using LMCCore.Game.Model;
using LMCCore.Utils;

namespace LMCCore.Game.Versioning;

internal sealed class LocalVersionRenamer(string? versionConfigsPath = null)
{
    private readonly string _versionConfigsPath = versionConfigsPath ??
        Path.Combine(Current.LMCPath, "version_configs.json");

    public void Rename(LocalGameVersionEntry version, string newName)
    {
        ArgumentNullException.ThrowIfNull(version);
        ValidateVersionName(newName);

        var oldName = version.VersionName;
        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            return;
        }

        var versionsDirectory = Path.GetFullPath(Path.Combine(version.RootPath, "versions"));
        var sourceDirectory = Path.GetFullPath(version.VersionDirectory);
        var targetDirectory = Path.Combine(versionsDirectory, newName);
        EnsureDirectChild(versionsDirectory, sourceDirectory);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Version directory '{sourceDirectory}' was not found.");
        }

        if (Directory.Exists(targetDirectory))
        {
            throw new IOException($"Version '{newName}' already exists.");
        }

        var mainJsonUpdate = PrepareMainJsonUpdate(sourceDirectory, oldName, newName);
        var inheritedVersionUpdates = PrepareInheritedVersionUpdates(
            versionsDirectory,
            sourceDirectory,
            oldName,
            newName);
        var configUpdate = PrepareConfigUpdate(sourceDirectory, targetDirectory);
        var movedDirectory = false;

        try
        {
            Directory.Move(sourceDirectory, targetDirectory);
            movedDirectory = true;
            RenameVersionOwnedPath(targetDirectory, oldName, newName, ".json");
            RenameVersionOwnedPath(targetDirectory, oldName, newName, ".jar");
            RenameVersionOwnedPath(targetDirectory, oldName, newName, "-natives");

            if (mainJsonUpdate != null)
            {
                WriteTextAtomically(Path.Combine(targetDirectory, $"{newName}.json"), mainJsonUpdate.UpdatedContent);
            }

            foreach (var update in inheritedVersionUpdates)
            {
                WriteTextAtomically(update.Path, update.UpdatedContent);
            }

            if (configUpdate != null)
            {
                WriteTextAtomically(_versionConfigsPath, configUpdate.UpdatedContent);
            }
        }
        catch
        {
            RestoreTextUpdates(inheritedVersionUpdates);
            if (configUpdate != null)
            {
                RestoreTextUpdate(configUpdate);
            }

            if (movedDirectory && Directory.Exists(targetDirectory))
            {
                TryRenameVersionOwnedPath(targetDirectory, newName, oldName, ".json");
                TryRenameVersionOwnedPath(targetDirectory, newName, oldName, ".jar");
                TryRenameVersionOwnedPath(targetDirectory, newName, oldName, "-natives");
                if (mainJsonUpdate != null)
                {
                    RestoreTextUpdate(mainJsonUpdate with
                    {
                        Path = Path.Combine(targetDirectory, $"{oldName}.json")
                    });
                }
                if (!Directory.Exists(sourceDirectory))
                {
                    Directory.Move(targetDirectory, sourceDirectory);
                }
            }

            throw;
        }
    }

    internal static void ValidateVersionName(string versionName)
    {
        if (!VersionNameValidator.IsValid(versionName))
        {
            throw new ArgumentException("The version name is not valid.", nameof(versionName));
        }
    }

    private static TextUpdate? PrepareMainJsonUpdate(string sourceDirectory, string oldName, string newName)
    {
        var jsonPath = Path.Combine(sourceDirectory, $"{oldName}.json");
        if (!File.Exists(jsonPath))
        {
            return null;
        }

        var originalContent = File.ReadAllText(jsonPath);
        var json = JsonNode.Parse(originalContent) as JsonObject
                   ?? throw new InvalidDataException($"Version json '{jsonPath}' is not a json object.");
        json["id"] = newName;
        return new TextUpdate(jsonPath, originalContent, json.ToJsonString(JsonUtils.DefaultSerializerOptions));
    }

    private static IReadOnlyList<TextUpdate> PrepareInheritedVersionUpdates(
        string versionsDirectory,
        string sourceDirectory,
        string oldName,
        string newName)
    {
        var updates = new List<TextUpdate>();
        foreach (var directory in Directory.EnumerateDirectories(versionsDirectory))
        {
            if (string.Equals(directory, sourceDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var versionName = Path.GetFileName(directory);
            var jsonPath = Path.Combine(directory, $"{versionName}.json");
            if (!File.Exists(jsonPath))
            {
                continue;
            }

            var originalContent = File.ReadAllText(jsonPath);
            JsonObject? json;
            try
            {
                json = JsonNode.Parse(originalContent) as JsonObject;
            }
            catch (System.Text.Json.JsonException)
            {
                continue;
            }

            if (json?["inheritsFrom"] is not JsonValue inheritsFromValue ||
                !inheritsFromValue.TryGetValue<string>(out var inheritsFrom) ||
                !string.Equals(inheritsFrom, oldName, StringComparison.Ordinal))
            {
                continue;
            }

            json["inheritsFrom"] = newName;
            updates.Add(new TextUpdate(
                jsonPath,
                originalContent,
                json.ToJsonString(JsonUtils.DefaultSerializerOptions)));
        }

        return updates;
    }

    private TextUpdate? PrepareConfigUpdate(string sourceDirectory, string targetDirectory)
    {
        if (!File.Exists(_versionConfigsPath))
        {
            return null;
        }

        var originalContent = File.ReadAllText(_versionConfigsPath);
        if (JsonNode.Parse(originalContent) is not JsonObject root)
        {
            throw new InvalidDataException($"Version config file '{_versionConfigsPath}' is not a json object.");
        }

        var oldPath = VersionPathUtils.NormalizePath(sourceDirectory);
        var property = root.FirstOrDefault(item =>
            string.Equals(VersionPathUtils.NormalizePathOrEmpty(item.Key), oldPath, StringComparison.OrdinalIgnoreCase));
        if (property.Key == null)
        {
            return null;
        }

        root.Remove(property.Key);
        root[VersionPathUtils.NormalizePath(targetDirectory)] = property.Value?.DeepClone();
        return new TextUpdate(
            _versionConfigsPath,
            originalContent,
            root.ToJsonString(JsonUtils.DefaultSerializerOptions));
    }

    private static void RenameVersionOwnedPath(
        string versionDirectory,
        string oldName,
        string newName,
        string suffix)
    {
        var oldPath = Path.Combine(versionDirectory, oldName + suffix);
        if (!File.Exists(oldPath) && !Directory.Exists(oldPath))
        {
            return;
        }

        var newPath = Path.Combine(versionDirectory, newName + suffix);
        if (File.Exists(oldPath))
        {
            File.Move(oldPath, newPath);
        }
        else
        {
            Directory.Move(oldPath, newPath);
        }
    }

    private static void TryRenameVersionOwnedPath(
        string versionDirectory,
        string oldName,
        string newName,
        string suffix)
    {
        try
        {
            RenameVersionOwnedPath(versionDirectory, oldName, newName, suffix);
        }
        catch (IOException)
        {
        }
    }

    private static void RestoreTextUpdates(IEnumerable<TextUpdate> updates)
    {
        foreach (var update in updates)
        {
            RestoreTextUpdate(update);
        }
    }

    private static void RestoreTextUpdate(TextUpdate update)
    {
        try
        {
            WriteTextAtomically(update.Path, update.OriginalContent);
        }
        catch (IOException)
        {
        }
    }

    private static void WriteTextAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, content);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static void EnsureDirectChild(string parentDirectory, string childDirectory)
    {
        var actualParent = Path.GetDirectoryName(childDirectory);
        if (!string.Equals(parentDirectory, actualParent, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The version directory is outside the managed versions directory.");
        }
    }

    private sealed record TextUpdate(string Path, string OriginalContent, string UpdatedContent);
}
