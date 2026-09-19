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

namespace LMCCore.Game.Model.Configuration;

public sealed class GameVersionJavaConfig
{
    public bool? AutoSelectJava { get; set; }

    public string? SelectedJavaPath { get; set; }

    public bool? AutoAllocateMemory { get; set; }

    public int? MaxMemoryMb { get; set; }

    public string? JvmArguments { get; set; }

    public string? WrapperArguments { get; set; }
}
