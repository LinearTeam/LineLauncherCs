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

using LMCCore.Game.Mods.Local;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace LMCUI.Pages.VersionManagePage.Mods;

public sealed class ModListItem(LocalModFile file, string stateText) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public LocalModFile File { get; private set; } = file;
    public IReadOnlyList<string> Badges { get; private set; } = BuildBadges(file);
    public string StateText { get; private set; } = stateText;
    public string Name => File.Name;
    public string FileName => File.FileName;
    public bool IsEnabled => File.IsEnabled;
    public bool HasMetadataError => File.HasMetadataError;

    public void Update(LocalModFile file, string stateText)
    {
        if (File == file && StateText == stateText)
            return;
        // Toggling a mod must not recreate its metadata controls.
        if (File.Version != file.Version || File.Loader != file.Loader)
            Badges = BuildBadges(file);
        File = file;
        StateText = stateText;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public override string ToString() => Name;

    private static IReadOnlyList<string> BuildBadges(LocalModFile file) =>
        new[] { file.Version, file.Loader }.OfType<string>()
            .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
}
