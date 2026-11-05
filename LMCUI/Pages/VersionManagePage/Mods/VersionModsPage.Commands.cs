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
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using LMCCore.Game.Mods.Local;
using LMCCore.Utils;

namespace LMCUI.Pages.VersionManagePage.Mods;

public partial class VersionModsPage
{
    private LocalModFile[] GetSelectedMods() =>
        ModList?.SelectedItems?.OfType<ModListItem>().Select(item => item.File).ToArray()
        ?? Array.Empty<LocalModFile>();

    private void UpdateSelectionCommands()
    {
        var count = GetSelectedMods().Length;
        PageCommands.IsVisible = count == 0;
        SelectionCommands.IsVisible = count > 0;
        ModCountText.Text = count > 0
            ? Text("SelectedCount", count, _mods.Count)
            : Text("Count", _mods.Count, _mods.Count(mod => mod.IsEnabled));
    }

    private void ModList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingList)
            UpdateSelectionCommands();
    }

    private async void EnableSelected_OnClick(object? sender, RoutedEventArgs e) =>
        await SetModsEnabledAsync(GetSelectedMods(), true);

    private async void DisableSelected_OnClick(object? sender, RoutedEventArgs e) =>
        await SetModsEnabledAsync(GetSelectedMods(), false);

    private Task SetModsEnabledAsync(LocalModFile[] mods, bool enabled) =>
        RunMutationAsync(async manager =>
        {
            var results = await manager.SetEnabledAsync(mods, enabled);
            var updated = mods.Zip(results).ToDictionary(pair => pair.First.FilePath, pair => pair.Second,
                StringComparer.Ordinal);
            _mods = _mods.Select(mod => updated.GetValueOrDefault(mod.FilePath, mod)).ToArray();
            foreach (var item in _visibleMods)
                if (updated.TryGetValue(item.File.FilePath, out var file))
                    UpdateModItem(item, file);
        }, rescan: false);

    private async void DeleteSelected_OnClick(object? sender, RoutedEventArgs e) =>
        await ConfirmDeleteAsync(GetSelectedMods());

    private void SelectAll_OnClick(object? sender, RoutedEventArgs e) => ModList.SelectAll();
    private void ClearSelection_OnClick(object? sender, RoutedEventArgs e) => ModList.SelectedItems?.Clear();

    private void ModList_OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (_isBusy)
            return;
        var row = e.Source as ListBoxItem
            ?? (e.Source as Visual)?.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        var atPointer = e.TryGetPosition(ModList, out _);
        var item = row?.DataContext as ModListItem;
        if (item == null && !atPointer)
            item = ModList.SelectedItem as ModListItem;
        if (item == null)
            return;

        if (ModList.SelectedItems?.Contains(item) != true)
        {
            ModList.SelectedItems?.Clear();
            ModList.SelectedItems?.Add(item);
        }

        var contextFile = item.File;
        var selected = GetSelectedMods();
        var reveal = new FAMenuFlyoutItem
        {
            Text = Text("RevealInFolder"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.OpenFolder }
        };
        reveal.Click += async (_, _) =>
        {
            try
            {
                await SystemFileOperations.RevealFileAsync(contextFile.FilePath);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        };
        var delete = new FAMenuFlyoutItem
        {
            Text = Text("DeleteMods"),
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Delete }
        };
        delete.Click += async (_, _) => await ConfirmDeleteAsync(selected);
        var menu = new FAMenuFlyout();
        menu.Items.Add(reveal);
        menu.Items.Add(delete);
        menu.ShowAt(row ?? (Control)ModList, atPointer);
        e.Handled = true;
    }

    private async Task ConfirmDeleteAsync(LocalModFile[] mods)
    {
        if (_isBusy || mods.Length == 0)
            return;
        var recycle = SystemFileOperations.SupportsRecycleBin;
        var dialog = new FAContentDialog
        {
            Title = Text("DeleteTitle"),
            Content = mods.Length == 1
                ? Text(recycle ? "RecycleDescription" : "PermanentDeleteDescription", mods[0].Name)
                : Text(recycle ? "RecycleManyDescription" : "PermanentDeleteManyDescription", mods.Length),
            PrimaryButtonText = Text(recycle ? "MoveToRecycleBin" : "DeletePermanently"),
            CloseButtonText = Text("Cancel"),
            DefaultButton = FAContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == FAContentDialogResult.Primary)
            await RunMutationAsync(async manager =>
            {
                await manager.DeleteAsync(mods);
                var paths = mods.Select(mod => mod.FilePath).ToHashSet(StringComparer.Ordinal);
                _mods = _mods.Where(mod => !paths.Contains(mod.FilePath)).ToArray();
            }, rescan: false);
    }
}
