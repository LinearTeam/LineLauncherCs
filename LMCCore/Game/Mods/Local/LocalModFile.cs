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

namespace LMCCore.Game.Mods.Local;

public sealed record LocalModFile(
    string FilePath, string Name, string? Version, string? Loader, bool IsEnabled, bool HasMetadataError)
{
    public string FileName => Path.GetFileName(FilePath);
    public string EnabledFilePath => IsEnabled ? FilePath : FilePath[..^9];
}
