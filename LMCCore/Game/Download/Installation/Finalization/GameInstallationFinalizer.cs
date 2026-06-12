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

using LMC;
using LMCCore.Game.Model;
using LMCCore.Game.Model.Configuration;
using LMCCore.Game.Versioning;

namespace LMCCore.Game.Download.Installation.Finalization;

public class GameInstallationFinalizer
{
    public async virtual Task FinalizeAsync(DownloadInstallationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        await context.CacheManager.CopyCacheToVersionDirectoryAsync(
            context.Request.RootPath,
            context.Request.VersionName,
            cancellationToken);

        var vcm = new VersionConfigManager();
        var path = Path.Combine(context.Request.RootPath, "versions", context.Request.VersionName);
        var entry = new LocalGameVersionEntry
        {
            ClientVersionId = context.Request.VersionId,
            JarPath = Path.Combine(path, $"{context.Request.VersionName}.jar"),
            JsonPath = Path.Combine(path, $"{context.Request.VersionName}.json"),
            Status = VersionStatus.Valid,
            VersionName = context.Request.VersionName,
            RootPath = context.Request.RootPath,
            VersionDirectory = path,
            VersionInfo = context.GetRequiredVersionInfo()
        };
        
        vcm.WriteIndependentConfig(entry, new GameVersionConfig
        {
            Game = new GameVersionGameConfig
            {
                ModLoaders = context.Request.Loaders,
                OptiFine = context.Request.OptiFine,
                VersionId = context.Request.VersionId
            }
        }, Current.Config.DefaultVersionConfigSourceForNewInstalls);
    }
}
