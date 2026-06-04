namespace LMC.Extensions.UI;

public class UIExtensionNavigationItem
{
    public required string Tag { get; init; }

    public required string Title { get; init; }

    public UIExtensionNavigationLocation Location { get; init; } = UIExtensionNavigationLocation.Menu;

    public string? ParentTag { get; init; }
}
