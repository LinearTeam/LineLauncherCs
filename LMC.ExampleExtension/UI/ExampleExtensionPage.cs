using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LMC.ExampleExtension.Runtime;
using LMCUI.I18n;
using LMCUI.Pages;

namespace LMC.ExampleExtension.UI;

public sealed class ExampleExtensionPage : PageBase
{
    private readonly TextBlock _headerText;
    private readonly TextBlock _descriptionText;
    private readonly TextBlock _summaryText;
    private readonly TextBlock _countsText;

    public ExampleExtensionPage() : base("Pages.LMCExampleExtension.Title", "LMCExampleExtensionPage")
    {
        _headerText = CreateTextBlock(fontSize: 24, fontWeight: FontWeight.SemiBold);
        _descriptionText = CreateTextBlock();
        _summaryText = CreateTextBlock();
        _countsText = CreateTextBlock();

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 12,
                Children =
                {
                    _headerText,
                    _descriptionText,
                    _summaryText,
                    _countsText
                }
            }
        };

        RefreshContent();
    }

    public override void OnNavigatedTo()
    {
        RefreshContent();
    }

    private void RefreshContent()
    {
        var snapshot = ExampleExtensionState.CreateSnapshot();
        _headerText.Text = I18nManager.Instance.GetString("Pages.LMCExampleExtension.Header");
        _descriptionText.Text = I18nManager.Instance.GetString("Pages.LMCExampleExtension.Description");
        _summaryText.Text = string.Join(
            Environment.NewLine,
            [
                $"{I18nManager.Instance.GetString("Pages.LMCExampleExtension.Fields.ExtensionVersion")}: {snapshot.ExtensionVersion}",
                $"{I18nManager.Instance.GetString("Pages.LMCExampleExtension.Fields.HostVersion")}: {snapshot.HostVersion}",
                $"{I18nManager.Instance.GetString("Pages.LMCExampleExtension.Fields.LastEvent")}: {GetEventDisplayName(snapshot.LastEvent)}"
            ]);

        _countsText.Text = string.Join(
            Environment.NewLine,
            BuildCounterLines(snapshot));
    }

    private IEnumerable<string> BuildCounterLines(ExampleExtensionSnapshot snapshot)
    {
        yield return I18nManager.Instance.GetString("Pages.LMCExampleExtension.Fields.HookCounts");

        foreach (var eventName in GetKnownEvents())
        {
            snapshot.EventCounts.TryGetValue(eventName, out var count);
            yield return $"{GetEventDisplayName(eventName)}: {count}";
        }
    }

    private static TextBlock CreateTextBlock(double fontSize = 14, FontWeight? fontWeight = null)
    {
        return new TextBlock
        {
            FontSize = fontSize,
            FontWeight = fontWeight ?? FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static string GetEventDisplayName(string eventName)
    {
        return I18nManager.Instance.GetString($"Pages.LMCExampleExtension.Events.{eventName}");
    }

    private static IReadOnlyList<string> GetKnownEvents()
    {
        return
        [
            "Initialize",
            "HostInitializing",
            "HostInitialized",
            "BeforeAccountAdd",
            "AfterAccountAdd",
            "AfterAccountRemove",
            "AfterAccountsLoaded",
            "BeforeManagedRootAdd",
            "AfterManagedRootAdded",
            "AfterSelectedRootChanged",
            "AfterVersionsScanned",
            "AfterParentTaskAdded"
        ];
    }
}
