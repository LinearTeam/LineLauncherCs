using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;

namespace LMCUI.Pages.VersionManagePage;

using LaunchPage;

public partial class VersionManagePage : PageBase
{
    public VersionManagePage() : base("Pages.VersionManagePage.Title","VersionManagePage")
    {
        InitializeComponent();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        MainWindow.NavigatePage(new PageNavigateWay(
            typeof(LaunchPage),
            (NavigationViewItem)MainWindow.Instance.mnv.SelectedItem
            ), NavigateType.Append);
    }
}