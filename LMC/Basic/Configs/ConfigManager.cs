/* *确保我以后能看懂这玩意怎么用
 *
 *  关于注解的用法：
 *    在类上使用[ConfigVersion({配置文件版本})]以指示当前配置文件的版本
 *
 *    在属性上使用[ConfigAlias("旧版本属性名", sinceVersion: {改属性名开始使用的版本, 可选},
 *                          untilVersion: {该属性名不再使用的版本, 可选})]以声明某个属性的属性名曾修改过
 *
 *    在属性上使用[ConfigRemoved(sinceVersion: {该属性自某个版本起删除})]以指示在迁移时直接删除该属性
 * 
 *    若配置文件中有无法匹配任何注解或属性名的键，则该键将被保留在配置文件中并忽略
 *
 *    如果需要兼容更复杂的配置文件变动，可以新建一个方法，并使用[ConfigUpgradeProcessor({原版本, 新版本})]
 *         以声明在从某个旧版本升级到某个新版本时需要进行的步骤
 *
 *    配置文件版本号始终为整数，在迁移时会对每个版本号尝试迁移，如果你希望声明一个自定义处理器，确保两个版本是相邻的版本，
 * 尽量不要为跨越多个版本的配置文件编写处理器。如果你真的要这么做，将其拆分为多个相邻的版本，直至某（些）属性没有变动的版本
 * 
 */

namespace LMC.Basic.Configs;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Logger=Logging.Logger;

#region 注解
[AttributeUsage(AttributeTargets.Class)]
public class ConfigVersionAttribute(int version) : Attribute {
    public int Version { get; } = version;

}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class ConfigAliasAttribute(string alias, int untilVersion = int.MaxValue) : Attribute {
    public string Alias { get; } = alias;
    public int? UntilVersion { get; } = untilVersion;

}

[AttributeUsage(AttributeTargets.Method)]
public class ConfigUpgradeProcessorAttribute(int fromVersion, int toVersion) : Attribute {
    public int FromVersion { get; } = fromVersion;
    public int ToVersion { get; } = toVersion;

}

[AttributeUsage(AttributeTargets.Property)]
public class ConfigRemovedAttribute(int sinceVersion) : Attribute {
    public int SinceVersion { get; } = sinceVersion;

}
#endregion

public static class ConfigManager {
    private const string VersionProperty = "$version";
    private static readonly ConcurrentDictionary<string, object> s_configLocks = new();
    private static Logger s_logger = new Logger("ConfigManager");
    
    static readonly JsonSerializerOptions s_serializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver() // 基于反射的解析器
    };

    public static T Load<T>(string configName) where T : new() {
        string filePath = GetConfigPath(configName);
        object configLock = s_configLocks.GetOrAdd(configName, _ => new object());

        lock (configLock) {
            if (!File.Exists(filePath)) {
                var defaultConfig = new T();
                SaveInternal(configName, defaultConfig, filePath);
                return defaultConfig;
            }

            try {
                string json = File.ReadAllText(filePath);
                var jsonObject = JsonNode.Parse(json) as JsonObject;

                if (jsonObject == null) return new T();

                int configVersion = jsonObject.TryGetPropertyValue(VersionProperty, out var versionNode)
                    ? versionNode?.GetValue<int>() ?? 1
                    : 1;

                int targetVersion = GetConfigVersion(typeof(T));
                if (configVersion != targetVersion) {
                    MigrateConfiguration(jsonObject, configVersion, targetVersion);
                }

                jsonObject.Remove(VersionProperty);
                return jsonObject.Deserialize<T>(s_serializerOptions) ?? new T();
            } catch {
                return new T();
            }
        }
    }

    public static void Save<T>(string configName, T config) {
        string filePath = GetConfigPath(configName);
        object configLock = s_configLocks.GetOrAdd(configName, _ => new object());
        
        lock (configLock) {
            SaveInternal(configName, config, filePath);
        }
    }

    private static void SaveInternal<T>(string configName, T config, string filePath) {
        string configDir = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(configDir);

        var jsonObject = JsonObject.Create(JsonSerializer.SerializeToElement(config, s_serializerOptions));
        if (jsonObject == null) return;

        jsonObject[VersionProperty] = GetConfigVersion(typeof(T));
        File.WriteAllText(filePath, jsonObject.ToJsonString(s_serializerOptions));
    }

    static string GetConfigPath(string configName) {
        return Path.Combine(Current.LMCPath, $"{configName}.config.json");
    }

    static int GetConfigVersion(Type configType) {
        var versionAttr = configType.GetCustomAttribute<ConfigVersionAttribute>();
        return versionAttr?.Version ?? 1;
    }

    static void MigrateConfiguration(JsonObject config, int currentVersion, int targetVersion) {
        if (currentVersion > targetVersion)
        {
            return;
        }

        while (currentVersion < targetVersion)
        {
            int nextVersion = currentVersion + 1;
            MigrateToNextVersion(config, currentVersion, nextVersion);
            currentVersion = nextVersion;
        }

        config[VersionProperty] = targetVersion;
    }

    static void MigrateToNextVersion(JsonObject config, int fromVersion, int toVersion) {
        ApplyAliasMapping(config, fromVersion, toVersion);

        ApplyCustomMigration(config, fromVersion, toVersion);

        CleanupRemovedFields(config, toVersion);
    }

    static void ApplyAliasMapping(JsonObject config, int fromVersion, int toVersion) {
        var aliasMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in AppDomain.CurrentDomain.GetAssemblies()
                     .SelectMany(a => a.GetTypes())
                     .Where(t => t.GetCustomAttribute<ConfigVersionAttribute>()?.Version == toVersion))
        {
            foreach (var prop in type.GetProperties())
            {
                var aliases = prop.GetCustomAttributes<ConfigAliasAttribute>()
                    .Where(a => a.UntilVersion >= fromVersion)
                    .Select(a => a.Alias);

                foreach (string alias in aliases)
                {
                    aliasMappings[alias] = prop.Name;
                }
            }
        }

        foreach ((string alias, string newName) in aliasMappings)
        {
            if (config.TryGetPropertyValue(alias, out var value) &&
                !config.ContainsKey(newName))
            {
                config[newName] = value;
            }
        }
    }

    static void ApplyCustomMigration(JsonObject config, int fromVersion, int toVersion) {
        var processors = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(m => m.GetCustomAttribute<ConfigUpgradeProcessorAttribute>() != null)
            .ToLookup(m => m.GetCustomAttribute<ConfigUpgradeProcessorAttribute>()!);

        foreach (var method in processors)
        {
            var attr = method.Key;
            if (attr.FromVersion == fromVersion && attr.ToVersion == toVersion)
            {
                var parameters = method.First().GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(JsonObject))
                {
                    method.First().Invoke(null,
                    [
                        config
                    ]);
                }
            }
        }
    }
    
    static void CleanupRemovedFields(JsonObject config, int currentVersion) {
        var removedFields = new List<string>();

        foreach (var type in AppDomain.CurrentDomain.GetAssemblies()
                     .SelectMany(a => a.GetTypes()))
        {
            foreach (var prop in type.GetProperties())
            {
                var removedAttr = prop.GetCustomAttribute<ConfigRemovedAttribute>();
                if (removedAttr != null && removedAttr.SinceVersion <= currentVersion)
                {
                    removedFields.Add(prop.Name);
                }
            }
        }

        foreach (string field in removedFields)
        {
            config.Remove(field);
        }
    }


}
