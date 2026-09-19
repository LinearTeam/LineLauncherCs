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

namespace LMCCore.Game.Launching.Configuration;

public readonly record struct SystemMemoryInfo(long TotalBytes, long AvailableBytes)
{
    public long UsedBytes => Math.Max(0, TotalBytes - AvailableBytes);

    public int GetRecommendedAllocationMb()
    {
        var totalMb = Math.Max(1, TotalBytes / 1024 / 1024);
        var availableMb = Math.Max(1, AvailableBytes / 1024 / 1024);
        var upperBoundMb = Math.Max(1024, totalMb * 3 / 4);
        return (int)Math.Clamp(availableMb / 2, 1024, upperBoundMb);
    }
}
