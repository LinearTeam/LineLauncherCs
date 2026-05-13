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

using System;
using LMCCore.Game.Model;
using LMCCore.Game.Versioning;
using LMCUI.I18n;

namespace LMCUI.Pages.VersionManagePage;

public partial class VersionDetailPage : PageBase
{
    private LocalGameVersionEntry? _version;

    public VersionDetailPage() : base("Pages.VersionDetailPage.Title", "VersionDetailPage")
    {
        InitializeComponent();
    }

    public override void ProcessParameter(object? param)
    {
        base.ProcessParameter(param);
        if (param is not LocalGameVersionEntry version)
        {
            return;
        }

        _version = version;
        Title = I18nManager.Instance.GetString("Pages.VersionDetailPage.Title", version.VersionName);
        VersionNameText.Text = version.VersionName;

        var typeText = version.Status != VersionStatus.Valid
            ? I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.Error")
            : GetVersionTypeDisplayText(version);
        var clientVersionIdText = string.Equals(version.ClientVersionId, "未知版本", StringComparison.Ordinal)
            ? I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.UnknownClientVersion")
            : version.ClientVersionId;
        VersionTypeText.Text = $"{typeText} - {clientVersionIdText}";
        VersionStatusText.Text = I18nManager.Instance.GetString("Pages.VersionDetailPage.Status", GetStatusText(version.Status));
        VersionRootText.Text = I18nManager.Instance.GetString("Pages.VersionDetailPage.RootPath", version.RootPath);
        VersionDirectoryText.Text = I18nManager.Instance.GetString("Pages.VersionDetailPage.VersionDirectory", version.VersionDirectory);
    }

    private string GetVersionTypeDisplayText(LocalGameVersionEntry version)
    {
        var displayType = version.VersionInfo == null
            ? GameVersionDisplayType.Release
            : GameVersionTypeClassifier.ClassifyManifestVersion(
                version.VersionInfo.Id,
                version.VersionInfo.Type,
                version.VersionInfo.ReleaseTime,
                version.VersionName,
                version.ClientVersionId);

        return displayType switch
        {
            GameVersionDisplayType.Snapshot => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.Snapshot"),
            GameVersionDisplayType.AprilFools => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.AprilFools"),
            GameVersionDisplayType.Old => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.Old"),
            _ => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionType.Release")
        };
    }

    private string GetStatusText(VersionStatus status)
    {
        return status switch
        {
            VersionStatus.Valid => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionStatus.Valid"),
            VersionStatus.MissingJar => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionStatus.MissingJar"),
            VersionStatus.MissingJson => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionStatus.MissingJson"),
            VersionStatus.InvalidJson => I18nManager.Instance.GetString("Pages.VersionManagePage.VersionStatus.InvalidJson"),
            _ => status.ToString()
        };
    }
}
