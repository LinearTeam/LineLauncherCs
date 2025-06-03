using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;

namespace LMCUI.Pages.VersionManagePage;

public partial class VersionManagePage : PageBase
{
    public VersionManagePage() : base("版本管理","VersionManagePage")
    {
        InitializeComponent();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        MainWindow.NavigatePage(new PageNavigateWay(
            typeof(LaunchPage.LaunchPage),
            (NavigationViewItem)MainWindow.Instance.mnv.SelectedItem
            ), NavigateType.Append);
    }
}