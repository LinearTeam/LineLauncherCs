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
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using LMC;
using LMC.Basic.Logging;
using LMCUI.I18n;

namespace LMCUI.Pages.SettingsPage.About;

public partial class CopyrightPage : PageBase
{
    private readonly Logger _logger = new("CopyrightPage");
    private bool _isDocumentOpen;

    public CopyrightPage() : base("Pages.CopyrightPage.Title", "CopyrightPage")
    {
        InitializeComponent();
        Loaded += (_, _) => ProjectExpander.Description = I18nManager.Instance.GetString(
            "Pages.AboutPage.AboutLauncherExpander.VersionDescription",
            Current.VersionType, Current.Version, Current.BuildNumber);
    }

    private async void ReadLicense_OnClick(object? sender, RoutedEventArgs e)
    {
        await ShowDocumentAsync("Pages.CopyrightPage.Project.License.Title", ProjectLegalDocuments.ReadLicenseAsync);
    }

    private async void ReadNotice_OnClick(object? sender, RoutedEventArgs e)
    {
        await ShowDocumentAsync("Pages.CopyrightPage.Project.Notice.Title", ProjectLegalDocuments.ReadNoticeAsync);
    }

    private async Task ShowDocumentAsync(string titleKey, Func<Task<string>> readText)
    {
        if (_isDocumentOpen)
        {
            return;
        }

        _isDocumentOpen = true;
        try
        {
            string text;
            try
            {
                text = await readText();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Reading bundled legal document");
                text = I18nManager.Instance.GetString("Pages.CopyrightPage.Project.DocumentReadFailed");
            }

            var dialog = new FAContentDialog
            {
                Title = I18nManager.Instance.GetString(titleKey),
                CloseButtonText = I18nManager.Instance.GetString("Pages.CopyrightPage.Project.Close"),
                Content = new SelectableTextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Left,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    FontSize = 14
                }
            };
            dialog.Resources["ContentDialogMaxWidth"] = 760d;
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Showing legal document dialog");
        }
        finally
        {
            _isDocumentOpen = false;
        }
    }
}
