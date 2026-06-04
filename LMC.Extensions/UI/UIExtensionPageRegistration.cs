namespace LMC.Extensions.UI;

public class UIExtensionPageRegistration
{
    public required Type PageType { get; init; }

    public string? StaticTag { get; init; }

    public UIExtensionPageStorageMode StorageMode { get; init; } = UIExtensionPageStorageMode.Singleton;

    public bool SupportsDynamicTag { get; init; }

    public string? DynamicTagPrefix { get; init; }

    public Func<object?, string>? GetCacheKey { get; init; }
}
