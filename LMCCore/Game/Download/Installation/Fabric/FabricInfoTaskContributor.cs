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

using System.Diagnostics;
using LMCCore.Game.Model.Loaders;
using LMCCore.Game.Model.LocalVersion.Libraries;
using LMCCore.Utils;

namespace LMCCore.Game.Download.Installation.Fabric;

public class FabricInfoTaskContributor : IInstallationSubTaskContributor, IGameInstallationVersionJsonModifier
{
    public void AddTasks(DownloadInstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var loader = context.GetRequiredLoader(ModLoaderType.Fabric);
        var task = context.ParentTask.CreateSubTask(
            $"获取Fabric信息 ({loader.VersionId})",
            -10,
            async (cancellationToken, _, progress) =>
            {
                var fabricVersionJson = await GetFabricVersionJsonAsync(
                    context,
                    loader.VersionId,
                    cancellationToken);
                progress.Report(100);
                return fabricVersionJson;
            },
            translationKey: "Pages.TaskPage.Tasks.GameInstall.Fabric.GetVersionInfo");

        context.Tasks.SetFabricVersionJsonTask(task);
        context.Tasks.RegisterVersionJsonDependencyTask(task);
    }

    public Task<string> ModifyVersionJsonAsync(
        string versionJson,
        DownloadInstallationContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionJson);
        ArgumentNullException.ThrowIfNull(context);

        var fabricVersionJson = context.Tasks.FabricVersionJsonTask?.Result
                                ?? throw new InvalidOperationException(
                                    "Fabric version json task has not completed.");
        return Task.FromResult(MergeVersionJson(versionJson, fabricVersionJson));
    }

    async private static Task<string> GetFabricVersionJsonAsync(
        DownloadInstallationContext context,
        string loaderVersion,
        CancellationToken cancellationToken)
    {
        var originUrl = $"https://meta.fabricmc.net/v2/versions/loader/{context.Request.VersionId}/{loaderVersion}/profile/json";
        var url = context.DownloadSourceManager.TransformUrl(originUrl) ?? originUrl;
        using var response = await HttpUtils.CreateRequest(url)
            .GetAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string MergeVersionJson(string vanillaVersionJson, string fabricVersionJson)
    {
        var vanilla = JsonUtils.Parse(vanillaVersionJson);
        var fabric = JsonUtils.Parse(fabricVersionJson);
        return vanilla.Merge(fabric, ["inheritsFrom", "id", "time", "releaseTime"]).ToString();
    }
}
