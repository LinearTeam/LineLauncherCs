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
using System.Collections.Generic;
using System.Linq;
using LMCUI.I18n;
using LMCCore.Game.Model;
using LMCCore.Game.Model.LocalVersion;

namespace LMCUI.Pages;

internal static class VersionSummaryPresentation
{
    public static string Build(
        LocalGameVersionEntry version,
        string unknownVersionText,
        bool includeMinecraftLabel = true)
    {
        var minecraftVersion = string.Equals(
            version.ClientVersionId,
            LocalGameVersionEntry.UnknownClientVersionId,
            StringComparison.Ordinal)
            ? unknownVersionText
            : version.ClientVersionId;

        var parts = new List<string>
        {
            includeMinecraftLabel
                ? GetString("Pages.VersionSummary.Minecraft", minecraftVersion)
                : minecraftVersion
        };
        parts.AddRange(version.ModLoaders.Select(loader =>
            $"{GetString(GetLoaderNameKey(loader.Type))} {loader.VersionId}"));
        return string.Join(", ", parts);
    }

    private static string GetString(string key, params object[] args)
    {
        var value = I18nManager.Instance.GetString(key);
        if (value == key)
        {
            value = key switch
            {
                "Pages.VersionSummary.Minecraft" => "Minecraft {0}",
                "Pages.VersionSummary.Fabric" => "Fabric",
                "Pages.VersionSummary.Forge" => "Forge",
                "Pages.VersionSummary.OptiFine" => "OptiFine",
                _ => key
            };
        }

        return args.Length == 0 ? value : string.Format(value, args);
    }

    private static string GetLoaderNameKey(LocalGameModLoaderType type)
    {
        return type switch
        {
            LocalGameModLoaderType.Fabric => "Pages.VersionSummary.Fabric",
            LocalGameModLoaderType.Forge => "Pages.VersionSummary.Forge",
            LocalGameModLoaderType.OptiFine => "Pages.VersionSummary.OptiFine",
            _ => type.ToString()
        };
    }
}
