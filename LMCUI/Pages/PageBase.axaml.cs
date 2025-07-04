﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LMCUI.Pages;

public partial class PageBase : UserControl
{
    public string Title { get; set; }
    public PageBase(string title, string tag)
    {
        Tag = tag;
        this.Title = title;
        InitializeComponent();
    }
}