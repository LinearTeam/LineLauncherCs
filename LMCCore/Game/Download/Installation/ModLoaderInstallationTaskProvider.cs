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
using LMCCore.Game.Model.Loaders;

namespace LMCCore.Game.Download.Installation;

public abstract class ModLoaderInstallationTaskProvider : IGameInstallationTaskProvider
{
    public abstract ModLoaderType LoaderType { get; }

    public DownloadInstallationComponent Component =>
        LoaderType switch
        {
            ModLoaderType.Fabric => DownloadInstallationComponent.Fabric,
            ModLoaderType.Forge => DownloadInstallationComponent.Forge,
            _ => throw new NotSupportedException($"Unsupported loader type '{LoaderType}'.")
        };

    public bool ShouldApply(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.HasLoader(LoaderType);
    }

    public abstract void AddTasks(DownloadInstallationContext context);
}
