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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LMC.Help;
using LMC.Help.Models;

namespace LMCUI.Pages.Help;

internal enum HelpIconRenderKind
{
    None,
    BuiltIn,
    Url,
    Assets
}

internal enum HelpItemRenderKind
{
    Markdown,
    Section
}

internal sealed record HelpIconRenderData(HelpIconRenderKind Kind, string? Value);

internal sealed record HelpItemRenderData(
    HelpItemRenderKind Kind,
    string Title,
    string Description,
    HelpIconRenderData Icon,
    string? Markdown,
    HelpContentPageParam? NavigationTarget);

internal sealed record HelpLoadResult(HelpFile HelpFile, Exception? Exception)
{
    public bool Success => Exception is null;
}

internal static class HelpPageSupport
{
    public static string GetHelpFilePath(string baseDirectory)
    {
        return Path.Combine(baseDirectory, "Assets", "help.yaml");
    }

    public async static Task<HelpLoadResult> LoadHelpFileAsync(string path)
    {
        try
        {
            return new HelpLoadResult(await HelpParser.ParseYamlFile(path), null);
        }
        catch (Exception ex)
        {
            return new HelpLoadResult(new HelpFile(), ex);
        }
    }

    public static IReadOnlyList<HelpItemRenderData> BuildRenderItems(string currentTag, IEnumerable<BaseHelpItem> helpItems)
    {
        return helpItems
            .Select(item => BuildRenderItem(currentTag, item))
            .OfType<HelpItemRenderData>()
            .ToList();
    }

    public static HelpContentPageParam BuildSectionNavigationTarget(string currentTag, SectionHelpItem sectionItem)
    {
        return new HelpContentPageParam(sectionItem.Title, $"{currentTag}.{sectionItem.Key}", [.. sectionItem.Helps]);
    }

    private static HelpItemRenderData? BuildRenderItem(string currentTag, BaseHelpItem helpItem)
    {
        return helpItem switch
        {
            MarkdownHelpItem markdown => new HelpItemRenderData(
                HelpItemRenderKind.Markdown,
                markdown.Title,
                markdown.Description,
                ToIconRenderData(markdown.Icon),
                markdown.Text,
                null),
            SectionHelpItem section => new HelpItemRenderData(
                HelpItemRenderKind.Section,
                section.Title,
                section.Description,
                ToIconRenderData(section.Icon),
                null,
                BuildSectionNavigationTarget(currentTag, section)),
            _ => null
        };
    }

    private static HelpIconRenderData ToIconRenderData(IconInfo? iconInfo)
    {
        if (iconInfo == null)
        {
            return new HelpIconRenderData(HelpIconRenderKind.None, null);
        }

        var kind = iconInfo.Type switch
        {
            IconType.BuiltIn => HelpIconRenderKind.BuiltIn,
            IconType.Url => HelpIconRenderKind.Url,
            IconType.Assets => HelpIconRenderKind.Assets,
            _ => HelpIconRenderKind.None
        };

        return new HelpIconRenderData(kind, iconInfo.Content);
    }
}
