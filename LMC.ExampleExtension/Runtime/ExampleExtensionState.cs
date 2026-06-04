using LMC.Extensions.Abstractions;

namespace LMC.ExampleExtension.Runtime;

internal static class ExampleExtensionState
{
    private static readonly object s_syncRoot = new();
    private static readonly Dictionary<string, int> s_eventCounts = new(StringComparer.OrdinalIgnoreCase);
    private static string s_hostVersion = "unknown";
    private static string s_extensionVersion = "unknown";
    private static string s_lastEvent = "None";

    public static void Initialize(ILMCExtensionContext context)
    {
        lock (s_syncRoot)
        {
            s_hostVersion = context.HostVersion;
            s_extensionVersion = context.ExtensionVersion;
            RecordEventUnsafe("Initialize");
        }
    }

    public static void RecordEvent(string eventName)
    {
        lock (s_syncRoot)
        {
            RecordEventUnsafe(eventName);
        }
    }

    public static ExampleExtensionSnapshot CreateSnapshot()
    {
        lock (s_syncRoot)
        {
            return new ExampleExtensionSnapshot
            {
                HostVersion = s_hostVersion,
                ExtensionVersion = s_extensionVersion,
                LastEvent = s_lastEvent,
                EventCounts = s_eventCounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            };
        }
    }

    private static void RecordEventUnsafe(string eventName)
    {
        s_lastEvent = eventName;
        s_eventCounts[eventName] = s_eventCounts.GetValueOrDefault(eventName) + 1;
    }
}
