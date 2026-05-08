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
using LMCUI.Pages;

namespace LMCUI.Navigation;

/// <summary>
/// 页面注册信息
/// </summary>
public class PageRegistration
{
    /// <summary>
    /// 页面类型
    /// </summary>
    public required Type PageType { get; init; }

    /// <summary>
    /// 静态标签（用于导航匹配），如果有的话
    /// </summary>
    public string? StaticTag { get; init; }

    /// <summary>
    /// 页面存储模式
    /// </summary>
    public PageStorageMode StorageMode { get; init; }

    /// <summary>
    /// 是否支持动态 Tag 匹配（如前缀匹配）
    /// </summary>
    public bool SupportsDynamicTag { get; init; }

    /// <summary>
    /// 动态 Tag 的前缀（当 SupportsDynamicTag 为 true 时使用）
    /// </summary>
    public string? DynamicTagPrefix { get; init; }

    /// <summary>
    /// 创建页面实例的工厂方法
    /// </summary>
    public Func<object?, PageBase>? Factory { get; init; }

    /// <summary>
    /// 从参数获取缓存键（用于 Parameterized 模式）
    /// </summary>
    public Func<object?, string>? GetCacheKey { get; init; }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public PageRegistration() { }

    /// <summary>
    /// 创建注册信息
    /// </summary>
    public PageRegistration(Type pageType, string? staticTag, PageStorageMode storageMode)
    {
        PageType = pageType;
        StaticTag = staticTag;
        StorageMode = storageMode;
    }

    /// <summary>
    /// 创建支持动态 Tag 的注册信息
    /// </summary>
    public static PageRegistration CreateDynamic(Type pageType, string tagPrefix, PageStorageMode storageMode = PageStorageMode.Transient)
    {
        return new PageRegistration
        {
            PageType = pageType,
            SupportsDynamicTag = true,
            DynamicTagPrefix = tagPrefix,
            StorageMode = storageMode
        };
    }
}
