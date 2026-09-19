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

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMCCore.Game.Model;
using LMCCore.Game.Versioning;
using LMCUI.I18n;
using LMCUI.Navigation;
using LMCUI.Navigation.Model;
using LMCUI.Pages;
using LMCUI.Utils;

namespace LMCUI.Pages.VersionManagePage;

public partial class VersionDetailPage : PageBase
{
    private readonly Logger _logger = new("VersionDetailPage");
    private readonly VersionManager _versionManager = new();
    private LocalGameVersionEntry? _version;

    public VersionDetailPage() : base("Pages.VersionDetailPage.Title", "VersionDetailPage")
    {
        InitializeComponent();
    }

    public override void ProcessParameter(object? param)
    {
        base.ProcessParameter(param);
        if (param is not LocalGameVersionEntry version)
        {
            return;
        }

        _version = version;
        Title = I18nManager.Instance.GetString("Pages.VersionDetailPage.Title", version.VersionName);
        VersionSummaryExpander.Header = version.VersionName;
        VersionSummaryExpander.Description = VersionSummaryPresentation.Build(
            version,
            I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.UnknownClientVersion"));
        VersionSummaryExpander.IconSource = new FAImageIconSource
        {
            Source = LoadVersionIcon(version)
        };
    }

    private void GameSettingsExpander_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_version == null || MainWindow.Instance.mnv.SelectedItem is not FANavigationViewItem selectedItem)
        {
            return;
        }

        MainWindow.NavigatePage(
            new PageNavigateWay(typeof(VersionGameSettingsPage), _version, selectedItem),
            NavigateType.Append);
    }

    private async void RenameVersionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_version == null)
        {
            return;
        }

        var versionToRename = _version;
        var versionNameBox = new TextBox
        {
            Text = versionToRename.VersionName,
            MinWidth = 320,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var errorText = new TextBlock
        {
            IsVisible = false,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        var dialog = new FAContentDialog
        {
            Title = I18nManager.Instance.GetString("Pages.VersionDetailPage.RenameDialog.Title"),
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    versionNameBox,
                    errorText
                }
            },
            PrimaryButtonText = I18nManager.Instance.GetString("Pages.VersionDetailPage.RenameDialog.Confirm"),
            CloseButtonText = I18nManager.Instance.GetString("Pages.VersionDetailPage.RenameDialog.Cancel"),
            DefaultButton = FAContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            var newName = versionNameBox.Text ?? string.Empty;
            if (!VersionNameValidator.IsValid(newName))
            {
                errorText.Text = I18nManager.Instance.GetString(
                    "Pages.VersionDetailPage.RenameDialog.InvalidName");
                errorText.IsVisible = true;
                versionNameBox.Focus();
                return;
            }

            dialog.IsPrimaryButtonEnabled = false;
            versionNameBox.IsEnabled = false;
            errorText.IsVisible = false;

            try
            {
                var renamedVersion = await _versionManager.RenameVersionAsync(versionToRename, newName);
                _version = renamedVersion;
                ProcessParameter(renamedVersion);
                var versions = await _versionManager.GetCachedOrScanVersionsAsync(renamedVersion.RootPath);
                VersionCatalogRefreshCoordinator.Publish(renamedVersion.RootPath, versions);
                dialog.Hide();
                await MessageQueueHelper.ShowSuccess(
                    I18nManager.Instance.GetString("Pages.VersionDetailPage.RenameDialog.SuccessTitle"),
                    I18nManager.Instance.GetString(
                        "Pages.VersionDetailPage.RenameDialog.SuccessContent",
                        renamedVersion.VersionName));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Renaming version '{versionToRename.VersionName}' to '{newName}'");
                errorText.Text = I18nManager.Instance.GetString(
                    "Pages.VersionDetailPage.RenameDialog.Failed",
                    ex.Message);
                errorText.IsVisible = true;
                dialog.IsPrimaryButtonEnabled = true;
                versionNameBox.IsEnabled = true;
                versionNameBox.Focus();
            }
        };
        await dialog.ShowAsync();
    }

    private static Bitmap LoadVersionIcon(LocalGameVersionEntry version)
    {
        var displayType = VersionManagePagePresentation.GetDisplayType(version);
        var iconPath = VersionManagePagePresentation.GetBuiltInIconResourcePath(displayType);
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            iconPath = "/Assets/VersionIcons/release.png";
        }

        using var stream = AssetLoader.Open(new Uri($"avares://LMCUI{iconPath}"));
        return new Bitmap(stream);
    }
}
