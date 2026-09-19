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
namespace LMC.Basic.Configs;

using System.Text.Json.Nodes;

internal static class ConfigMigrationPipeline
{
    public static void Migrate(JsonObject config, int currentVersion, int targetVersion)
    {
        if (currentVersion > targetVersion)
        {
            return;
        }

        while (currentVersion < targetVersion)
        {
            var nextVersion = currentVersion + 1;
            ApplyAliasMappings(config, currentVersion, nextVersion);
            ApplyUpgradeProcessors(config, currentVersion, nextVersion);
            CleanupRemovedFields(config, nextVersion);
            currentVersion = nextVersion;
        }
    }

    private static void ApplyAliasMappings(JsonObject config, int fromVersion, int toVersion)
    {
        foreach (var mapping in ConfigMetadataRepository.GetAliasMappings(fromVersion, toVersion))
        {
            if (config.TryGetPropertyValue(mapping.Key, out var value) && !config.ContainsKey(mapping.Value))
            {
                config[mapping.Value] = value?.DeepClone();
            }
        }
    }

    private static void ApplyUpgradeProcessors(JsonObject config, int fromVersion, int toVersion)
    {
        foreach (var processor in ConfigMetadataRepository.GetUpgradeProcessors(fromVersion, toVersion))
        {
            processor(config);
        }
    }

    private static void CleanupRemovedFields(JsonObject config, int currentVersion)
    {
        foreach (var field in ConfigMetadataRepository.GetRemovedFields(currentVersion))
        {
            config.Remove(field);
        }
    }
}
