using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LMCUI.Pages.SettingsPage;

using Avalonia.Interactivity;
using GameSettings;

public partial class SettingsPage : PageBase
{
    public SettingsPage() : base("Pages.SettingsPage.Title", "SettingsPage")
    {
        InitializeComponent();
    }
    void GameSettingsExpander_OnClick(object? sender, RoutedEventArgs e) {
        MainWindow.NavigatePage(new PageNavigateWay(typeof(GameSettingsPage), 
            MainWindow.Instance.mnv.SettingsItem), NavigateType.Append);
    }
}

