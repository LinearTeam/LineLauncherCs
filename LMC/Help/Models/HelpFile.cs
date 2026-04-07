namespace LMC.Help.Models;

public abstract class BaseHelpItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public HelpType Type { get; set; }
    public IconInfo Icon { get; set; } = new();
}
public enum HelpType
{
    Markdown,
    Section
}
public class IconInfo
{
    public IconType Type { get; set; }
    public string? Content { get; set; } = string.Empty;
}

public enum IconType
{
    BuiltIn = 0,    // 使用内置IconSource
    Url = 1,        // 使用Url
    Assets = 2      // 使用Avalonia AssetsUrl
}

public enum ButtonActionType
{
    OpenUrl = 0,
    OpenFile = 1,
    CheckUpdate = 3
}

public enum RepoType
{
    Common = 0,     // 通用
    CSharp = 1,     // C#版
    Vb = 2          // VB版
}

public class ButtonInfo
{
    public ButtonActionType Type { get; set; }
    public string? Content { get; set; } = string.Empty;
    public string? Action { get; set; } = string.Empty;
    public bool IsDefault { get; set; } = false;
}

public class MarkdownHelpItem : BaseHelpItem
{
    public RepoType Repo { get; set; }
    public int Button { get; set; }  // 按钮的数量，0 代表没有可供交互的按钮
    public string Text { get; set; } = string.Empty;
    public List<ButtonInfo> Buttons { get; set; } = new();
}

public class SectionHelpItem : BaseHelpItem
{
    public List<BaseHelpItem> Helps { get; set; } = new();
}

public class HelpFile
{
    public List<BaseHelpItem> Helps { get; set; } = new();
}
