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
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMCCore.Game.Mods.Local;
using LMCCore.Game.Versioning;
using LMCUI.I18n;
using LMCUI.Pages;
using LMCUI.Pages.VersionManagePage.Mods;
using LMCUI.Pages.DownloadMinecraftPage;
using LMCUI.Utils;

namespace LMCUI;

public partial class MainWindow
{
    private bool _isHandlingDrop;
    private bool _isDragInWindow;
    private int _dragEventSequence;

    private void InitializeFileDrop()
    {
        DragDrop.SetAllowDrop(this, true);
        // Avalonia drag events only bubble. Observe them even when a child marks them handled.
        AddHandler(DragDrop.DragEnterEvent, MainWindow_OnDragEnter, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DragLeaveEvent, MainWindow_OnDragLeave, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DragOverEvent, MainWindow_OnDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, MainWindow_OnDrop, RoutingStrategies.Bubble, handledEventsToo: true);
        s_logger.Info("File drag-and-drop handlers initialized (bubble routing).");
    }

    private void MainWindow_OnDragEnter(object? sender, DragEventArgs e)
    {
        _dragEventSequence++;
        if (!_isDragInWindow)
        {
            _isDragInWindow = true;
            try
            {
                var paths = GetDroppedPaths(e);
                s_logger.Info($"Drag entered launcher: {paths.Length} local file(s); " +
                              $"formats: [{string.Join(", ", e.DataTransfer.Formats)}]; " +
                              $"paths: [{string.Join("; ", paths)}].");
                if (GetDropRejectionReason() is { } reason)
                    s_logger.Info($"File drag cannot currently be accepted: {reason}.");
            }
            catch (Exception ex)
            {
                s_logger.Error(ex, "Reading files dragged into launcher");
            }
        }

        MainWindow_OnDragOver(sender, e);
    }

    private void MainWindow_OnDragLeave(object? sender, DragEventArgs e)
    {
        // A child-to-child move raises Leave then Enter in the same dispatch. Native Leave
        // can carry the last in-window position, so coordinates cannot identify a real exit.
        var sequence = ++_dragEventSequence;
        Dispatcher.UIThread.Post(() =>
        {
            if (_dragEventSequence == sequence)
                _isDragInWindow = false;
        }, DispatcherPriority.Input);
    }

    private void MainWindow_OnDragOver(object? sender, DragEventArgs e)
    {
        // Do not resolve native file payloads on every pointer movement.
        e.DragEffects = GetDropRejectionReason() == null && e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void MainWindow_OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        _isDragInWindow = false;
        s_logger.Info("File drop received by launcher.");
        if (GetDropRejectionReason() is { } reason)
        {
            e.DragEffects = DragDropEffects.None;
            s_logger.Warn($"File drop ignored: {reason}.");
            return;
        }
        _isHandlingDrop = true;
        try
        {
            // Snapshot native data before the first await; it belongs to this drop event.
            var paths = GetDroppedPaths(e);
            s_logger.Info($"Dropped {paths.Length} local file(s): [{string.Join("; ", paths)}].");
            if (paths.Length == 0)
            {
                s_logger.Warn("File drop ignored: payload contains no local files.");
                return;
            }
            await ProcessDropAsync(paths);
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Processing dropped files");
            await MessageQueueHelper.ShowError(
                I18nManager.Instance.GetString("MainWindow.Import.ErrorTitle"),
                I18nManager.Instance.GetString("MainWindow.Import.OperationFailed", ex.Message));
        }
        finally
        {
            _isHandlingDrop = false;
        }
    }

    private async Task ProcessDropAsync(string[] paths)
    {
        var wasVersionContext = mainFrm.CurrentPage is IVersionContextPage;
        var target = (mainFrm.CurrentPage as IVersionContextPage)?.TargetVersion;
        var jars = paths.Where(path => path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)).ToArray();
        var packs = paths.Where(path => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                        || path.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (jars.Length > 0 && packs.Length > 0 || packs.Length > 1 || jars.Length + packs.Length != paths.Length)
        {
            s_logger.Warn("File drop rejected: unsupported files, mixed JAR/archive files, or multiple archives.");
            await MessageQueueHelper.ShowWarning(
                I18nManager.Instance.GetString("MainWindow.Import.WarningTitle"),
                I18nManager.Instance.GetString("MainWindow.Import.InvalidDropSet"));
            return;
        }

        if (packs.Length == 1)
        {
            s_logger.Info($"Opening modpack import for dropped archive: '{packs[0]}'.");
            await ModpackImportFlow.InstallAsync(packs[0]);
            return;
        }

        if (jars.Length == 0)
            return;

        if (target == null && !wasVersionContext)
        {
            target = await ResolveSelectedVersionAsync();
            if (target == null)
            {
                s_logger.Warn("Mod drop rejected: no explicitly selected launch version.");
                await MessageQueueHelper.ShowWarning(
                    I18nManager.Instance.GetString("MainWindow.Import.WarningTitle"),
                    I18nManager.Instance.GetString("MainWindow.Import.NoVersionContext"));
                return;
            }
        }

        if (target == null || target.Status != LMCCore.Game.Model.VersionStatus.Valid
                           || !System.IO.Directory.Exists(target.VersionDirectory)
                           || !System.IO.File.Exists(target.JsonPath))
        {
            s_logger.Warn("Mod drop rejected: target version is invalid or no longer exists.");
            await MessageQueueHelper.ShowWarning(
                I18nManager.Instance.GetString("MainWindow.Import.WarningTitle"),
                I18nManager.Instance.GetString("MainWindow.Import.InvalidTarget"));
            return;
        }

        if (!wasVersionContext)
        {
            var dialog = new FAContentDialog
            {
                Title = I18nManager.Instance.GetString("MainWindow.Import.ConfirmTitle"),
                Content = I18nManager.Instance.GetString(
                    "MainWindow.Import.ConfirmContent", target.VersionName, jars.Length),
                PrimaryButtonText = I18nManager.Instance.GetString("MainWindow.Import.Confirm"),
                CloseButtonText = I18nManager.Instance.GetString("MainWindow.Import.Cancel"),
                DefaultButton = FAContentDialogButton.Primary
            };
            if (await dialog.ShowAsync() != FAContentDialogResult.Primary)
            {
                s_logger.Info("Mod drop import cancelled at target confirmation.");
                return;
            }
        }

        var manager = new LocalModManager(target.VersionDirectory);
        s_logger.Info($"Importing {jars.Length} dropped mod(s) into '{target.VersionDirectory}'.");
        await ModImportDialogs.ImportAndNotifyAsync(manager, target.VersionName, jars);
    }

    private string? GetDropRejectionReason()
    {
        if (_isHandlingDrop)
            return "another file drop is being processed";
        if (!_isWindowLoaded || !_isSplashScreenCompleted || !_isInitialNavigationCompleted)
            return $"startup incomplete (loaded={_isWindowLoaded}, splash={_isSplashScreenCompleted}, " +
                   $"navigation={_isInitialNavigationCompleted})";
        var overlay = OverlayLayer.GetOverlayLayer(this);
        return overlay?.Children
            .OfType<FADialogHost>()
            .Any(host => host.Content is FAContentDialog) == true
            ? "a content dialog is open"
            : null;
    }

    private static string[] GetDroppedPaths(DragEventArgs e) =>
        e.DataTransfer.GetItems(DataFormat.File)
            .Select(item => item.TryGetValue(DataFormat.File))
            .OfType<IStorageFile>()
            .Select(file => file.TryGetLocalPath())
            .OfType<string>()
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();

    private static Task<LMCCore.Game.Model.LocalGameVersionEntry?> ResolveSelectedVersionAsync() =>
        new VersionManager().ResolveExplicitlySelectedLaunchVersionAsync();
}
