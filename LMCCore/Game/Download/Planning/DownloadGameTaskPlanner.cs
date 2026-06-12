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
using LMCCore.Game.Download.Model;
using LMCCore.Tasks;

namespace LMCCore.Game.Download.Planning;

internal sealed class DownloadGameTaskPlanner
{
    public DownloadGamePlan CreatePlan(DownloadableGameVersion request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parent = TaskManager.Instance.CreateParent(
            $"Install Minecraft {request.VersionName}",
            "Pages.TaskPage.Tasks.GameInstall.Parent",
            [request.VersionName]);
        return new DownloadGamePlan
        {
            Request = request,
            ParentTask = parent
        };
    }
}
