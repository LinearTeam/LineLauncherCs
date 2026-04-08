using Avalonia.Controls;

namespace LMCUI.Pages;

public partial class PageBase : UserControl
{
    public string Title { get; set; }
    protected PageBase(string title, string tag)
    {
        Tag = tag;
        this.Title = title;
        InitializeComponent();
    }

    public virtual void ProcessParameter(object? param)
    { }
}