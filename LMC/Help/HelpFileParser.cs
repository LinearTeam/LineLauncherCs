using LMC.Help.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LMC.Help;

public static class HelpParser
{
    public static HelpFile ParseYaml(string yamlContent)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var yamlData = deserializer.Deserialize<YamlHelpData>(yamlContent);
            
        return ConvertToHelpFile(yamlData);
    }

    public async static Task<HelpFile> ParseYamlFile(string filePath)
    {
        var yamlContent = await File.ReadAllTextAsync(filePath);
        return ParseYaml(yamlContent);
    }

    private static HelpFile ConvertToHelpFile(YamlHelpData? yamlData)
    {
        var helpFile = new HelpFile();

        if (yamlData?.Helps == null)
            return helpFile;
        
        foreach (var baseHelpItem in yamlData.Helps.Select(ConvertToBaseHelpItem).OfType<BaseHelpItem>())
        {
            helpFile.Helps.Add(baseHelpItem);
        }

        return helpFile;
    }

    private static BaseHelpItem? ConvertToBaseHelpItem(Dictionary<string, YamlHelpItem>? helpItemDict)
    {
        if (helpItemDict == null || helpItemDict.Count == 0)
            return null;

        foreach (var kvp in helpItemDict)
        {
            var yamlItem = kvp.Value;
            if(yamlItem.Type == null)
                continue;
            switch (yamlItem.Type.ToLower())
            {
                case "markdown":
                    return new MarkdownHelpItem
                    {
                        Key = kvp.Key,
                        Title = yamlItem.Title ?? string.Empty,
                        Type = HelpType.Markdown,
                        Icon = ConvertToIconInfo(yamlItem.Icon),
                        Repo = (RepoType)(yamlItem.Repo ?? 0),
                        Button = yamlItem.Button ?? 0,
                        Description = yamlItem.Description ?? string.Empty,
                        Text = yamlItem.Text ?? string.Empty,
                        Buttons = ConvertToButtonInfos(yamlItem.Buttons)
                    };

                case "section":
                {
                    var sectionItem = new SectionHelpItem
                    {
                        Key = kvp.Key,
                        Title = yamlItem.Title ?? string.Empty,
                        Type = HelpType.Section,
                        Icon = ConvertToIconInfo(yamlItem.Icon),
                        Description = yamlItem.Description ?? string.Empty,
                        Helps = []
                    };

                    if (yamlItem.Helps == null)
                        return sectionItem;
                    foreach (var subItem in yamlItem.Helps.Select(ConvertToBaseHelpItem).OfType<BaseHelpItem>())
                    {
                        sectionItem.Helps.Add(subItem);
                    }

                    return sectionItem;
                }
            }
        }
            
        return null;
    }

    private static IconInfo ConvertToIconInfo(YamlIconInfo? yamlIcon)
    {
        if (yamlIcon == null)
            return new IconInfo();

        return new IconInfo
        {
            Type = (IconType)(yamlIcon.Type ?? 0),
            Content = yamlIcon.Content ?? string.Empty
        };
    }

    private static List<ButtonInfo> ConvertToButtonInfos(List<YamlButtonInfo>? yamlButtons)
    {
        var buttons = new List<ButtonInfo>();

        if (yamlButtons == null)
            return buttons;
        buttons.AddRange(yamlButtons.Select(yamlButton => new ButtonInfo
        {
            Type = (ButtonActionType)(yamlButton.Type ?? 0),
            Content = yamlButton.Content ?? string.Empty,
            Action = yamlButton.Action ?? string.Empty,
            IsDefault = yamlButton.IsDefault ?? false
        }));

        return buttons;
    }
}

// YAML数据模型类
public class YamlHelpData
{
    public List<Dictionary<string, YamlHelpItem>>? Helps { get; set; }
}

public class YamlHelpItem
{
    public string? Title { get; set; }
    public string? Type { get; set; } // "markdown" or "section"
    public string? Description { get; set; }
    public int? Repo { get; set; }
    public int? Button { get; set; }
    public YamlIconInfo? Icon { get; set; }
    public string? Text { get; set; }
    public List<YamlButtonInfo>? Buttons { get; set; }
    public List<Dictionary<string, YamlHelpItem>>? Helps { get; set; } // 用于section类型
}

public class YamlIconInfo
{
    public int? Type { get; set; }
    public string? Content { get; set; }
}

public class YamlButtonInfo
{
    public int? Type { get; set; }
    public string? Content { get; set; }
    public string? Action { get; set; }
    public bool? IsDefault { get; set; }
}