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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using LMC.Basic.Logging;
using LMCUI.Pages;

namespace LMCUI.Navigation;

/// <summary>
/// 页面注册表，管理所有已注册页面的信息
/// </summary>
public class PageRegistry
{
    private readonly static Lazy<PageRegistry> s_instance = new(() => new PageRegistry());
    public static PageRegistry Instance => s_instance.Value;

    private readonly Logger _logger = new Logger("PageRegistry");
    private readonly Dictionary<Type, PageRegistration> _byType = new();
    private readonly Dictionary<string, PageRegistration> _byStaticTag = new();
    private readonly List<PageRegistration> _dynamicRegistrations = new();
    private readonly ConcurrentDictionary<(Type Type, string CacheKey), PageBase> _parameterizedCache = new();

    private PageRegistry()
    {
        RegisterBuiltInPages();
    }

    /// <summary>
    /// 注册内置页面
    /// </summary>
    private void RegisterBuiltInPages()
    {
        // 直接导航页面 - 单例模式（默认）
        Register<Pages.LaunchPage.LaunchPage>("LaunchPage");
        Register<Pages.VersionManagePage.VersionManagePage>("VersionManagePage");
        Register<Pages.DownloadMinecraftPage.DownloadMinecraftPage>("DownloadMinecraftPage");
        Register(typeof(Pages.VersionManagePage.VersionDetailPage), "VersionDetailPage", PageStorageMode.Parameterized);
        Register<Pages.AccountPage.AccountPage>("AccountPage");
        Register<Pages.TaskPage.TaskPage>("TaskPage");
        Register<Pages.SettingsPage.SettingsPage>("SettingsPage");
        Register<Pages.Help.HelpPage>("HelpPage");

        // 帮助内容页面 - 支持动态 Tag
        RegisterDynamic(typeof(Pages.Help.HelpContentPage), "HelpPage");

        // 设置子页面 - 单例模式（默认）
        Register<Pages.SettingsPage.LauncherSettings.LauncherSettingsPage>("LauncherSettingsPage");
        Register<Pages.SettingsPage.GameSettings.GameSettingsPage>("GameSettingsPage");
        Register<Pages.SettingsPage.AboutPage>("AboutPage");
        Register<Pages.SettingsPage.CopyrightPage>("CopyrightPage");

        _logger.Info("内置页面注册完成");
    }

    /// <summary>
    /// 注册页面（静态 Tag）
    /// </summary>
    public PageRegistration Register<TPage>(string staticTag, PageStorageMode storageMode = PageStorageMode.Singleton) where TPage : PageBase
    {
        return Register(typeof(TPage), staticTag, storageMode);
    }

    /// <summary>
    /// 注册页面（静态 Tag）
    /// </summary>
    public PageRegistration Register(Type pageType, string staticTag, PageStorageMode storageMode = PageStorageMode.Singleton)
    {
        var registration = new PageRegistration
        {
            PageType = pageType,
            StaticTag = staticTag,
            StorageMode = storageMode
        };
        return Register(registration);
    }

    /// <summary>
    /// 注册页面
    /// </summary>
    public PageRegistration Register(PageRegistration registration)
    {
        if (registration.PageType == null)
        {
            throw new ArgumentException("PageType cannot be null", nameof(registration));
        }

        _byType[registration.PageType] = registration;

        if (registration.StaticTag != null)
        {
            _byStaticTag[registration.StaticTag] = registration;
        }

        if (registration.SupportsDynamicTag)
        {
            _dynamicRegistrations.Add(registration);
        }

        _logger.Debug($"Registered page: {registration.PageType.Name}, Tag: {registration.StaticTag ?? "dynamic"}, Mode: {registration.StorageMode}");
        return registration;
    }

    /// <summary>
    /// 注册支持动态 Tag 的页面
    /// </summary>
    public PageRegistration RegisterDynamic(Type pageType, string tagPrefix, PageStorageMode storageMode = PageStorageMode.Transient)
    {
        var registration = PageRegistration.CreateDynamic(pageType, tagPrefix, storageMode);
        return Register(registration);
    }

    /// <summary>
    /// 通过 Type 获取注册信息
    /// </summary>
    public PageRegistration? GetByType(Type pageType)
    {
        return _byType.TryGetValue(pageType, out var registration) ? registration : null;
    }

    /// <summary>
    /// 通过静态 Tag 获取注册信息
    /// </summary>
    public PageRegistration? GetByStaticTag(string tag)
    {
        return _byStaticTag.TryGetValue(tag, out var registration) ? registration : null;
    }

    /// <summary>
    /// 通过 Tag 匹配注册信息（支持动态 Tag）
    /// </summary>
    public PageRegistration? MatchByTag(string tag)
    {
        // 首先尝试静态 Tag 匹配
        if (_byStaticTag.TryGetValue(tag, out var staticMatch))
        {
            return staticMatch;
        }

        // 然后尝试动态 Tag 前缀匹配
        foreach (var registration in _dynamicRegistrations)
        {
            if (registration.DynamicTagPrefix != null && tag.StartsWith(registration.DynamicTagPrefix))
            {
                return registration;
            }
        }

        return null;
    }

    /// <summary>
    /// 创建页面实例
    /// </summary>
    public PageBase CreateInstance(PageRegistration registration, object? param = null)
    {
        if (registration.Factory != null)
        {
            return registration.Factory(param);
        }

        // 根据存储模式处理
        switch (registration.StorageMode)
        {
            case PageStorageMode.Transient:
                return CreateTransientInstance(registration, param);

            case PageStorageMode.Singleton:
                return CreateOrGetSingleton(registration, param);

            case PageStorageMode.Parameterized:
                return CreateOrGetParameterized(registration, param);

            default:
                return CreateTransientInstance(registration, param);
        }
    }

    /// <summary>
    /// 创建瞬态实例（每次都新建）
    /// </summary>
    private PageBase CreateTransientInstance(PageRegistration registration, object? param)
    {
        var instance = (PageBase)Activator.CreateInstance(registration.PageType)!;
        if (param != null)
        {
            instance.ProcessParameter(param);
        }
        return instance;
    }

    /// <summary>
    /// 获取或创建单例实例
    /// </summary>
    private PageBase CreateOrGetSingleton(PageRegistration registration, object? param)
    {
        var cacheKey = $"_singleton_{registration.PageType.FullName}";
        return _parameterizedCache.GetOrAdd((registration.PageType, cacheKey), _ =>
        {
            var instance = (PageBase)Activator.CreateInstance(registration.PageType)!;
            if (param != null)
            {
                instance.ProcessParameter(param);
            }
            return instance;
        });
    }

    /// <summary>
    /// 获取或创建参数化实例
    /// </summary>
    private PageBase CreateOrGetParameterized(PageRegistration registration, object? param)
    {
        var cacheKey = registration.GetCacheKey?.Invoke(param) ?? param?.ToString() ?? "_null_";
        return _parameterizedCache.GetOrAdd((registration.PageType, cacheKey), _ =>
        {
            var instance = (PageBase)Activator.CreateInstance(registration.PageType)!;
            if (param != null)
            {
                instance.ProcessParameter(param);
            }
            return instance;
        });
    }

    /// <summary>
    /// 获取已缓存的实例（如果有）
    /// </summary>
    public PageBase? GetCachedInstance(Type pageType, object? param = null)
    {
        if (!_byType.TryGetValue(pageType, out var registration))
        {
            return null;
        }

        string cacheKey;
        switch (registration.StorageMode)
        {
            case PageStorageMode.Singleton:
                cacheKey = $"_singleton_{pageType.FullName}";
                break;
            case PageStorageMode.Parameterized:
                cacheKey = registration.GetCacheKey?.Invoke(param) ?? param?.ToString() ?? "_null_";
                break;
            default:
                return null;
        }

        return _parameterizedCache.TryGetValue((pageType, cacheKey), out var instance) ? instance : null;
    }

    /// <summary>
    /// 清除参数化缓存（可选）
    /// </summary>
    public void ClearParameterizedCache()
    {
        var keysToRemove = _parameterizedCache.Keys.Where(k => !k.CacheKey.StartsWith("_singleton_")).ToList();
        foreach (var key in keysToRemove)
        {
            _parameterizedCache.TryRemove(key, out _);
        }
        _logger.Debug("已清除参数化缓存");
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public void ClearAllCache()
    {
        _parameterizedCache.Clear();
        _logger.Debug("已清除所有缓存");
    }
}
