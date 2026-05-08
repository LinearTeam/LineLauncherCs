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

using Avalonia.Controls;

namespace LMCUI.Pages;

/// <summary>
/// 页面基类
/// </summary>
public partial class PageBase : UserControl
{
    /// <summary>
    /// 页面标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 页面标签（可以是动态的）
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// 导航信息（导航到当前页面时传入）
    /// </summary>
    public NavigationInfo? NavigationInfo { get; protected set; }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    protected PageBase()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    protected PageBase(string title, string tag)
    {
        Tag = tag;
        Title = title;
        InitializeComponent();
    }

    /// <summary>
    /// 处理导航参数
    /// </summary>
    public virtual void ProcessParameter(object? param)
    {
        if (param is NavigationInfo navInfo)
        {
            NavigationInfo = navInfo;
            if (navInfo.Title != null)
            {
                Title = navInfo.Title;
            }
            if (navInfo.Tag != null)
            {
                Tag = navInfo.Tag;
            }
        }
    }

    /// <summary>
    /// 页面被导航到时调用
    /// </summary>
    public virtual void OnNavigatedTo()
    {
    }

    /// <summary>
    /// 页面被导航离开时调用
    /// </summary>
    public virtual void OnNavigatedFrom()
    {
    }
}

/// <summary>
/// 导航信息
/// </summary>
public class NavigationInfo
{
    /// <summary>
    /// 标题（可选）
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// 标签（可选）
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// 自定义参数
    /// </summary>
    public object? Param { get; init; }

    public NavigationInfo() { }

    public NavigationInfo(object? param)
    {
        Param = param;
    }

    public NavigationInfo(string title, string tag, object? param = null)
    {
        Title = title;
        Tag = tag;
        Param = param;
    }
}
