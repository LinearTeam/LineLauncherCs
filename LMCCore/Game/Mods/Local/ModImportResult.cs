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

public enum ModImportDecision
{
    Replace,
    Skip,
    Cancel
}

public sealed record ModImportConflict(string SourcePath, string DestinationPath, bool HasAmbiguousState);

public sealed record ModImportChoice(ModImportDecision Decision, bool ApplyToAll = false);

public sealed record ModImportResult(
    int ImportedCount,
    int SkippedCount,
    IReadOnlyList<string> Errors,
    bool Cancelled);
