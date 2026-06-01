using LMC.Help;
using LMC.Help.Models;
using LMCUI.Pages.Help;

namespace LineLauncherCs.Tests;

public class HelpSystemTests
{
    [Fact]
    public void GetHelpFilePath_UsesAssetsDirectoryUnderBaseDirectory()
    {
        var path = HelpPageSupport.GetHelpFilePath(@"D:\Apps\LMCUI\bin\Debug\net10.0");

        Assert.Equal(@"D:\Apps\LMCUI\bin\Debug\net10.0\Assets\help.yaml", path);
    }

    [Fact]
    public async Task LoadHelpFileAsync_ReturnsRecoverableFailureForMissingFile()
    {
        using var scope = new TestFileSystemScope();

        var result = await HelpPageSupport.LoadHelpFileAsync(scope.GetPath("missing-help.yaml"));

        Assert.False(result.Success);
        Assert.NotNull(result.Exception);
        Assert.Empty(result.HelpFile.Helps);
    }

    [Fact]
    public void ParseYaml_ParsesMarkdownAndSectionItems()
    {
        const string yaml = """
            helps:
              - welcome:
                  type: markdown
                  title: Welcome
                  description: Intro
                  text: "# Hello"
                  icon:
                    type: 0
                    content: Help
                  buttons:
                    - type: 0
                      content: Docs
                      action: https://example.com
                      isDefault: true
              - guides:
                  type: section
                  title: Guides
                  description: More
                  helps:
                    - child:
                        type: markdown
                        title: Child
                        text: Child text
            """;

        var helpFile = HelpParser.ParseYaml(yaml);

        Assert.Equal(2, helpFile.Helps.Count);
        var markdown = Assert.IsType<MarkdownHelpItem>(helpFile.Helps[0]);
        Assert.Equal("Welcome", markdown.Title);
        Assert.Single(markdown.Buttons);

        var section = Assert.IsType<SectionHelpItem>(helpFile.Helps[1]);
        Assert.Equal("guides", section.Key);
        Assert.Single(section.Helps);
    }

    [Fact]
    public void ParseYaml_IgnoresUnknownOrMissingTypes()
    {
        const string yaml = """
            helps:
              - unknown:
                  type: unsupported
                  title: Skip me
              - noType:
                  title: Missing type
            """;

        var helpFile = HelpParser.ParseYaml(yaml);

        Assert.Empty(helpFile.Helps);
    }

    [Fact]
    public void BuildRenderItems_CreatesSectionNavigationTarget()
    {
        var section = new SectionHelpItem
        {
            Key = "faq",
            Title = "FAQ",
            Description = "Questions",
            Helps =
            [
                new MarkdownHelpItem
                {
                    Key = "child",
                    Title = "Child",
                    Text = "Child text"
                }
            ]
        };

        var renderItems = HelpPageSupport.BuildRenderItems("HelpPage", [section]);

        var renderItem = Assert.Single(renderItems);
        Assert.Equal(HelpItemRenderKind.Section, renderItem.Kind);
        Assert.NotNull(renderItem.NavigationTarget);
        Assert.Equal("HelpPage.faq", renderItem.NavigationTarget!.Tag);
        Assert.Single(renderItem.NavigationTarget.HelpItems);
    }
}
