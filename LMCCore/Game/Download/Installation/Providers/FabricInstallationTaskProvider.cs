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
using LMCCore.Game.Download.Installation.Fabric;
using LMCCore.Game.Model.Loaders;

namespace LMCCore.Game.Download.Installation.Providers;

public sealed class FabricInstallationTaskProvider : ModLoaderInstallationTaskProvider, IGameInstallationVersionJsonModifier
{
    private readonly FabricInfoTaskContributor _versionJsonModifier = new();

    public override ModLoaderType LoaderType => ModLoaderType.Fabric;

    public override void AddTasks(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _versionJsonModifier.AddTasks(context);
    }

    public Task<string> ModifyVersionJsonAsync(
        string versionJson,
        DownloadInstallationContext context,
        CancellationToken cancellationToken)
    {
        return _versionJsonModifier.ModifyVersionJsonAsync(versionJson, context, cancellationToken);
    }
}
