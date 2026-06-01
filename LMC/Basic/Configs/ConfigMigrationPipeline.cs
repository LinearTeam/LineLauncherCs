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
