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

using System.Collections.Concurrent;
using System.Reflection;

internal static class ConfigMetadataRepository
{
    private readonly static object s_syncRoot = new();
    private static ConfigScanResult? s_cachedScanResult;
    private static int s_cachedAssemblyCount = -1;
    private readonly static ConcurrentDictionary<Type, int> s_configVersions = new();

    public static int GetConfigVersion(Type configType)
    {
        return s_configVersions.GetOrAdd(configType, static type =>
            type.GetCustomAttribute<ConfigVersionAttribute>()?.Version ?? 1);
    }

    public static IReadOnlyDictionary<string, string> GetAliasMappings(int fromVersion, int toVersion)
    {
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in GetScanResult().GetTypesByVersion(toVersion))
        {
            foreach (var property in GetPropertiesSafe(type))
            {
                foreach (var alias in GetCustomAttributesSafe<ConfigAliasAttribute>(property)
                             .Where(attribute => attribute.UntilVersion >= fromVersion))
                {
                    mappings[alias.Alias] = property.Name;
                }
            }
        }

        return mappings;
    }

    public static IReadOnlyList<Action<System.Text.Json.Nodes.JsonObject>> GetUpgradeProcessors(int fromVersion, int toVersion)
    {
        return GetScanResult().GetUpgradeProcessors(fromVersion, toVersion);
    }

    public static IReadOnlySet<string> GetRemovedFields(int currentVersion)
    {
        return GetScanResult().GetRemovedFields(currentVersion);
    }

    private static ConfigScanResult GetScanResult()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        lock (s_syncRoot)
        {
            if (s_cachedScanResult != null && s_cachedAssemblyCount == assemblies.Length)
            {
                return s_cachedScanResult;
            }

            s_cachedScanResult = ConfigScanResult.Create(assemblies);
            s_cachedAssemblyCount = assemblies.Length;
            return s_cachedScanResult;
        }
    }

    private static PropertyInfo[] GetPropertiesSafe(Type type)
    {
        try
        {
            return type.GetProperties();
        }
        catch
        {
            return [];
        }
    }

    private static MethodInfo[] GetMethodsSafe(Type type)
    {
        try
        {
            return type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }
        catch
        {
            return [];
        }
    }

    private static TAttribute? GetCustomAttributeSafe<TAttribute>(MemberInfo member)
        where TAttribute : Attribute
    {
        try
        {
            return member.GetCustomAttribute<TAttribute>();
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<TAttribute> GetCustomAttributesSafe<TAttribute>(MemberInfo member)
        where TAttribute : Attribute
    {
        try
        {
            return member.GetCustomAttributes<TAttribute>();
        }
        catch
        {
            return [];
        }
    }

    private sealed class ConfigScanResult(
        IReadOnlyDictionary<int, IReadOnlyList<Type>> configTypesByVersion,
        IReadOnlyDictionary<(int FromVersion, int ToVersion), IReadOnlyList<Action<System.Text.Json.Nodes.JsonObject>>> upgradeProcessors,
        IReadOnlyList<(int SinceVersion, string PropertyName)> removedFields)
    {
        private readonly IReadOnlyDictionary<int, IReadOnlyList<Type>> _configTypesByVersion = configTypesByVersion;
        private readonly IReadOnlyDictionary<(int FromVersion, int ToVersion), IReadOnlyList<Action<System.Text.Json.Nodes.JsonObject>>> _upgradeProcessors = upgradeProcessors;
        private readonly IReadOnlyList<(int SinceVersion, string PropertyName)> _removedFields = removedFields;

        public static ConfigScanResult Create(IEnumerable<Assembly> assemblies)
        {
            var configTypesByVersion = new Dictionary<int, List<Type>>();
            var upgradeProcessors = new Dictionary<(int FromVersion, int ToVersion), List<Action<System.Text.Json.Nodes.JsonObject>>>();
            var removedFields = new List<(int SinceVersion, string PropertyName)>();

            foreach (var assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type != null).Cast<Type>().ToArray();
                }

                foreach (var type in types)
                {
                    var versionAttribute = GetCustomAttributeSafe<ConfigVersionAttribute>(type);
                    if (versionAttribute != null)
                    {
                        if (!configTypesByVersion.TryGetValue(versionAttribute.Version, out var versionTypes))
                        {
                            versionTypes = [];
                            configTypesByVersion[versionAttribute.Version] = versionTypes;
                        }

                        versionTypes.Add(type);
                    }

                    foreach (var property in GetPropertiesSafe(type))
                    {
                        var removedAttribute = GetCustomAttributeSafe<ConfigRemovedAttribute>(property);
                        if (removedAttribute != null)
                        {
                            removedFields.Add((removedAttribute.SinceVersion, property.Name));
                        }
                    }

                    foreach (var method in GetMethodsSafe(type))
                    {
                        var processorAttribute = GetCustomAttributeSafe<ConfigUpgradeProcessorAttribute>(method);
                        if (processorAttribute == null)
                        {
                            continue;
                        }

                        var parameters = method.GetParameters();
                        if (parameters.Length != 1 || parameters[0].ParameterType != typeof(System.Text.Json.Nodes.JsonObject))
                        {
                            continue;
                        }

                        var key = (processorAttribute.FromVersion, processorAttribute.ToVersion);
                        if (!upgradeProcessors.TryGetValue(key, out var processors))
                        {
                            processors = [];
                            upgradeProcessors[key] = processors;
                        }

                        processors.Add(jsonObject => method.Invoke(null, [jsonObject]));
                    }
                }
            }

            return new ConfigScanResult(
                configTypesByVersion.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Type>)pair.Value.AsReadOnly()),
                upgradeProcessors.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Action<System.Text.Json.Nodes.JsonObject>>)pair.Value.AsReadOnly()),
                removedFields.AsReadOnly());
        }

        public IReadOnlyList<Type> GetTypesByVersion(int version)
        {
            return _configTypesByVersion.TryGetValue(version, out var types)
                ? types
                : [];
        }

        public IReadOnlyList<Action<System.Text.Json.Nodes.JsonObject>> GetUpgradeProcessors(int fromVersion, int toVersion)
        {
            return _upgradeProcessors.TryGetValue((fromVersion, toVersion), out var processors)
                ? processors
                : [];
        }

        public IReadOnlySet<string> GetRemovedFields(int currentVersion)
        {
            var removedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _removedFields)
            {
                if (entry.SinceVersion <= currentVersion)
                {
                    removedFields.Add(entry.PropertyName);
                }
            }

            return removedFields;
        }
    }
}
