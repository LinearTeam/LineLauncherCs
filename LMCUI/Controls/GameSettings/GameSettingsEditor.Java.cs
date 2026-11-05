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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMC;
using LMC.Basic;
using LMC.Basic.Configs;
using LMCCore.Java;
using LMCUI.I18n;
using LMCUI.Pages.SettingsPage.GameSettings;
using LMCUI.Utils;

namespace LMCUI.Controls.GameSettings;

public partial class GameSettingsEditor : UserControl
{
    private List<LocalJava> _javaInfos = [];
    private Task? _javaRefreshTask;
    private bool _javaRefreshRequested;

    public Task RefreshJavaItemsAsync()
    {
        _javaRefreshRequested = true;
        if (_javaRefreshTask is { IsCompleted: false })
        {
            return _javaRefreshTask;
        }

        _javaRefreshTask = RefreshJavaItemsCoreAsync();
        return _javaRefreshTask;
    }

    private async Task RefreshJavaItemsCoreAsync()
    {
        do
        {
            _javaRefreshRequested = false;
            await RefreshJavaItemsOnceAsync();
        } while (_javaRefreshRequested);
    }

    private async Task RefreshJavaItemsOnceAsync()
    {
        try
        {
            var javaPaths = Current.Config.JavaPaths.ToArray();
            var result = await Task.Run(async () =>
            {
                var javaInfos = new List<LocalJava>(javaPaths.Length);
                var invalidPaths = new List<string>();
                foreach (var javaPath in javaPaths)
                {
                    try
                    {
                        var javaInfo = await JavaManager.TryGetJavaInfo(javaPath);
                        if (javaInfo == null)
                        {
                            invalidPaths.Add(javaPath);
                        }
                        else
                        {
                            javaInfos.Add(javaInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        invalidPaths.Add(javaPath);
                        _logger.Warn($"Ignoring invalid Java path {javaPath}: {ex.Message}");
                    }
                }

                return (JavaInfos: javaInfos, InvalidPaths: invalidPaths);
            });

            _javaInfos = result.JavaInfos;
            if (result.InvalidPaths.Count > 0)
            {
                foreach (var invalidPath in result.InvalidPaths)
                {
                    Current.Config.JavaPaths.Remove(invalidPath);
                }

                if (result.InvalidPaths.Any(path => string.Equals(
                        path,
                        Current.Config.SelectedJavaPath,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    Current.Config.SelectedJavaPath = string.Empty;
                }

                try
                {
                    await Task.Run(() => ConfigManager.Save("app", Current.Config));
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Saving app config after pruning invalid Java paths");
                }

                try
                {
                    InvalidJavaPathsFound?.Invoke(result.InvalidPaths);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Handling invalid Java paths");
                }
            }

            UpdateJavaItems();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Refreshing Java items");
        }
    }

    private void UpdateJavaItems()
    {
        var enabledText = I18nManager.Instance.GetString(
            "Pages.SettingsPage.GameSettingsPage.JavaRuntime.JavaListExpander.JavaListItem.Enabled");
        var items = GameSettingsPagePresentation.BuildJavaItems(
            GameSettingsPagePresentation.BuildJavaItemViewData(
                _javaInfos,
                _settings.SelectedJavaPath,
                enabledText),
            Foreground!,
            Foreground!);

        JavaListExpander.Header = I18nManager.Instance.GetString(
            GameSettingsPagePresentation.GetJavaListHeaderKey(items.Count));
        JavaListExpander.ItemsSource = items;
    }

    private void SelectJava_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string java } ||
            !Current.Config.JavaPaths.Contains(java))
        {
            return;
        }

        ChangeSetting(GameSettingsField.SelectedJavaPath, _settings with { SelectedJavaPath = java });
        UpdateJavaItems();
    }

    private async void RemoveJava_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string java } || !Current.Config.JavaPaths.Contains(java))
        {
            return;
        }

        try
        {
            await Task.Run(() => JavaManager.RemoveJava(java));
            await RefreshJavaItemsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Removing Java");
        }
    }

    private async void SearchJava_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            var progress = new Action<TaskCallbackInfo>(info => Dispatcher.UIThread.Post(() =>
                JavaSearchStatus.Text = I18nManager.Instance.GetString(
                    "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchProgress",
                    info.Progress,
                    info.Total,
                    I18nManager.Instance.GetString(info.Message))));
            var javas = await Task.Run(() => JavaManager.SearchJava(progress));
            await Task.Run(() => JavaManager.AddJavasAsync(javas));
            JavaSearchStatus.Text = I18nManager.Instance.GetString(
                "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchSuccess");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Searching Java");
            JavaSearchStatus.Text = I18nManager.Instance.GetString(
                "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.SearchFailed",
                ex.Message);
        }
        finally
        {
            try
            {
                await RefreshJavaItemsAsync();
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
    }

    private async void AddJava_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            control.IsEnabled = false;
        }

        try
        {
            var executableName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";
            var typeFilter = new FilePickerFileType(executableName)
            {
                Patterns = [executableName]
            };
            var files = await CrossPlatformUtils.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = [typeFilter],
                Title = I18nManager.Instance.GetString(
                    "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.ChooseJavaDialog.Title")
            });
            var file = files.FirstOrDefault();
            if (file == null)
            {
                return;
            }

            var root = GameSettingsPagePresentation.ResolveJavaRootPath(file.Path.LocalPath);
            await Task.Run(() => JavaManager.AddJava(root));
            JavaSearchStatus.Text = I18nManager.Instance.GetString(
                "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.AddSuccess");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Adding Java");
            JavaSearchStatus.Text = I18nManager.Instance.GetString(
                "Pages.SettingsPage.GameSettingsPage.JavaRuntime.ImportExpander.StatusText.AddFailed",
                ex.Message);
        }
        finally
        {
            if (sender is Control importControl)
            {
                importControl.IsEnabled = !_isReadOnly;
            }
        }

        await RefreshJavaItemsAsync();
    }

    private void JavaItemExpander_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not FASettingsExpanderItem { Tag: string path })
        {
            return;
        }

        try
        {
            CrossPlatformUtils.OpenFolderInExplorer(Path.GetFullPath(path));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Opening Java folder");
        }
    }
}
