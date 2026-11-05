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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMCCore.Game.Download.Model;
using LMCCore.Game.Modpacks;
using LMCCore.Game.Versioning;

namespace LMCUI.Pages.DownloadMinecraftPage;

internal sealed class ModrinthModpackImportDialog
{
    private static readonly Logger s_logger = new("ModpackImportDialog");
    private sealed record RootItem(string Path)
    {
        public override string ToString() => Path;
    }

    public async Task<DownloadableGameVersion?> ShowAsync(string archivePath)
    {
        using var cancellation = new CancellationTokenSource();
        var versionManager = new VersionManager();
        var knownRoots = versionManager.GetManagedRoots().Select(root => root.RootPath).ToArray();
        var selectedRoot = versionManager.GetSelectedRoot()?.RootPath;
        var rootPicker = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsEnabled = false,
            ItemTemplate = new FuncDataTemplate<RootItem>((root, _) => new TextBlock
            {
                Text = root?.Path,
                TextTrimming = TextTrimming.CharacterEllipsis
            })
        };
        var nameBox = new TextBox { IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        var status = new TextBlock { Text = Text("Inspecting"), TextWrapping = TextWrapping.Wrap };
        var ring = new FAProgressRing { Width = 16, Height = 16, IsActive = true, IsIndeterminate = true };
        var selectedPaths = new HashSet<string>(StringComparer.Ordinal);
        var optionalLabel = new TextBlock { Text = Text("OptionalFiles"), IsVisible = false };
        var optionalList = new ListBox
        {
            Height = 180,
            IsVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<string>((path, _) =>
            {
                var check = new CheckBox
                {
                    Content = new TextBlock { Text = path, TextWrapping = TextWrapping.Wrap },
                    IsChecked = path != null && selectedPaths.Contains(path)
                };
                check.IsCheckedChanged += (_, _) =>
                {
                    if (path == null)
                        return;
                    if (check.IsChecked == true)
                        selectedPaths.Add(path);
                    else
                        selectedPaths.Remove(path);
                };
                return check;
            })
        };
        var form = new StackPanel
        {
            Spacing = 8,
            MaxWidth = 440,
            Children =
            {
                ring, status,
                new TextBlock { Text = Text("TargetFolder") }, rootPicker,
                new TextBlock { Text = Text("NewVersionName") }, nameBox,
                optionalLabel, optionalList
            }
        };
        var dialog = new FAContentDialog
        {
            Title = Text("Title"),
            Content = form,
            PrimaryButtonText = Text("Install"),
            CloseButtonText = Text("Cancel"),
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false
        };
        ModrinthModpackInfo? pack = null;
        DownloadableGameVersion? result = null;
        Task? submission = null;
        var closed = false;

        async Task SubmitAsync()
        {
            if (pack == null || rootPicker.SelectedItem is not RootItem root)
                return;
            var name = nameBox.Text?.Trim() ?? "";
            if (!VersionNameValidator.IsValid(name))
            {
                status.Text = Text("InvalidTarget");
                nameBox.Focus();
                return;
            }
            dialog.IsPrimaryButtonEnabled = false;
            var optionalSnapshot = new HashSet<string>(selectedPaths, StringComparer.Ordinal);
            try
            {
                var request = await Task.Run(() => ModrinthModpackInstaller.CreateRequest(
                    pack, root.Path, name, optionalSnapshot), cancellation.Token);
                if (closed || cancellation.IsCancellationRequested)
                    return;
                result = request;
                dialog.Hide();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex)
            {
                s_logger.Error(ex, "Preparing modpack installation");
                if (!closed)
                {
                    status.Text = ex is IOException ? Text("TargetExists") : Text("InvalidTarget");
                    dialog.IsPrimaryButtonEnabled = true;
                }
            }
        }

        dialog.PrimaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            if (pack != null && (submission == null || submission.IsCompleted))
                submission = SubmitAsync();
        };
        dialog.Closing += (_, _) =>
        {
            closed = true;
            cancellation.Cancel();
        };

        var shown = dialog.ShowAsync();
        try
        {
            var rootsTask = Task.Run(() => knownRoots.Where(Directory.Exists)
                .Select(path => new RootItem(path)).ToArray(), cancellation.Token);
            var packTask = ModrinthModpackReader.ReadAsync(archivePath, cancellation.Token);
            // Observe both tasks before releasing cancellation and archive resources.
            await Task.WhenAll(rootsTask, packTask);
            cancellation.Token.ThrowIfCancellationRequested();
            var roots = await rootsTask;
            if (roots.Length == 0)
                throw new InvalidOperationException(Text("NoRoots"));
            pack = await packTask;
            rootPicker.ItemsSource = roots;
            rootPicker.SelectedItem = roots.FirstOrDefault(root =>
                string.Equals(root.Path, selectedRoot, OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) ?? roots[0];
            rootPicker.IsEnabled = nameBox.IsEnabled = true;
            nameBox.Text = pack.Index.Name;
            var loader = pack.LoaderId switch
            {
                "fabric-loader" => Text("Fabric"),
                "forge" => Text("Forge"),
                _ => Text("Vanilla")
            };
            if (pack.LoaderVersion != null)
                loader += " " + pack.LoaderVersion;
            status.Text = Text("PackSummary", pack.Index.Name, pack.Index.VersionId, pack.MinecraftVersion, loader);
            if (!string.IsNullOrWhiteSpace(pack.Index.Summary))
                status.Text += Environment.NewLine + pack.Index.Summary;
            var optionalPaths = pack.OptionalFiles.Select(file => file.Path).ToArray();
            selectedPaths.UnionWith(optionalPaths);
            optionalList.ItemsSource = optionalPaths;
            optionalLabel.IsVisible = optionalList.IsVisible = optionalPaths.Length > 0;
            dialog.IsPrimaryButtonEnabled = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            s_logger.Error(ex, "Inspecting modpack archive");
            if (!closed)
            {
                status.Text = ModpackImportFlow.LocalizeReaderError(ex);
                dialog.CloseButtonText = Text("Close");
            }
        }
        finally
        {
            ring.IsVisible = ring.IsActive = false;
        }
        await shown;
        if (submission != null)
            await submission;
        return result;
    }

    private static string Text(string key, params object[] args) =>
        I18n.I18nManager.Instance.GetString("Pages.ModpackImport." + key, args);
}
