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

using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Layout;
using FluentAvalonia.UI.Controls;
using LMCCore.Game.Mods.Local;
using LMCUI.I18n;
using LMC.Basic.Logging;
using LMCUI.Utils;

namespace LMCUI.Pages.VersionManagePage.Mods;

internal static class ModImportDialogs
{
    private static readonly Logger s_logger = new("ModImport");

    public static async Task<ModImportResult> ImportAndNotifyAsync(
        LocalModManager manager, string versionName, IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        var result = await ImportAsync(manager, paths, cancellationToken);
        foreach (var error in result.Errors)
            s_logger.Error($"Mod import failed for '{manager.ModsDirectory}': {error}");
        if (result.ImportedCount == 0 && result.Errors.Count > 0)
            await MessageQueueHelper.ShowError(Text("ErrorTitle"), Text("AllFailed", result.Errors.Count));
        else if (result.SkippedCount > 0 || result.Errors.Count > 0 || result.Cancelled)
            await MessageQueueHelper.ShowWarning(Text("WarningTitle"), Text("Partial",
                versionName, result.ImportedCount, result.SkippedCount, result.Errors.Count));
        else if (result.ImportedCount > 0)
            await MessageQueueHelper.ShowSuccess(Text("SuccessTitle"),
                Text("SuccessContent", versionName, result.ImportedCount));
        return result;
    }

    private static string Text(string key, params object[] args) =>
        I18nManager.Instance.GetString("MainWindow.Import." + key, args);

    public static Task<ModImportResult> ImportAsync(
        LocalModManager manager, IEnumerable<string> paths, CancellationToken cancellationToken = default) =>
        manager.ImportAsync(paths, ResolveConflictAsync, cancellationToken);

    public static async Task<ModImportChoice> ResolveConflictAsync(
        ModImportConflict conflict, CancellationToken cancellationToken)
    {
        return await Dispatcher.UIThread.InvokeAsync(() => ShowConflictAsync(conflict, cancellationToken));
    }

    private static async Task<ModImportChoice> ShowConflictAsync(
        ModImportConflict conflict, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = conflict.HasAmbiguousState ? "AmbiguousConflict" : "Conflict";
        var applyAll = new CheckBox
        {
            Content = I18nManager.Instance.GetString("Pages.VersionModsPage.ImportDialog.ApplyToAll")
        };
        var dialog = new FAContentDialog
        {
            Title = I18nManager.Instance.GetString("Pages.VersionModsPage.ImportDialog.ConflictTitle"),
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = I18nManager.Instance.GetString(
                            $"Pages.VersionModsPage.ImportDialog.{key}",
                            System.IO.Path.GetFileName(conflict.SourcePath),
                            System.IO.Path.GetFileName(conflict.DestinationPath)),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    applyAll
                }
            },
            PrimaryButtonText = I18nManager.Instance.GetString("Pages.VersionModsPage.ImportDialog.Replace"),
            SecondaryButtonText = I18nManager.Instance.GetString("Pages.VersionModsPage.ImportDialog.Skip"),
            CloseButtonText = I18nManager.Instance.GetString("Pages.VersionModsPage.ImportDialog.Cancel"),
            DefaultButton = FAContentDialogButton.Close
        };
        using var registration = cancellationToken.Register(() => Dispatcher.UIThread.Post(dialog.Hide));
        var result = await dialog.ShowAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return new ModImportChoice(result switch
        {
            FAContentDialogResult.Primary => ModImportDecision.Replace,
            FAContentDialogResult.Secondary => ModImportDecision.Skip,
            _ => ModImportDecision.Cancel
        }, applyAll.IsChecked == true);
    }
}
