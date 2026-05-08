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
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using LMC.Basic.Logging;
using LMCUI.Pages;

namespace LMCUI.Navigation;

/// <summary>
/// 滑动方向
/// </summary>
public enum SlideDirection
{
    /// <summary>
    /// 从右侧滑入
    /// </summary>
    Right,

    /// <summary>
    /// 从左侧滑入
    /// </summary>
    Left,

    /// <summary>
    /// 从底部滑入
    /// </summary>
    Bottom,

    /// <summary>
    /// 从顶部滑入
    /// </summary>
    Top
}

/// <summary>
/// 自定义 Frame 控件，支持页面导航、缓存和历史管理
/// </summary>
public class LineFrame : ContentControl
{
    private readonly static Logger s_logger = new Logger("LineFrame");

    /// <summary>
    /// 当前页面
    /// </summary>
    public PageBase? CurrentPage { get; private set; }

    /// <summary>
    /// 导航历史栈
    /// </summary>
    private readonly Stack<NavigationHistoryEntry> _historyStack = new();

    /// <summary>
    /// 当前导航参数
    /// </summary>
    private object? _currentParam;

    /// <summary>
    /// 是否正在导航（防止重复导航）
    /// </summary>
    private bool _isNavigating;

    /// <summary>
    /// 是否正在返回（用于动画方向判断）
    /// </summary>
    private bool _isNavigatingBack;

    /// <summary>
    /// 导航完成事件
    /// </summary>
    public event EventHandler<NavigationEventArgs>? Navigated;

    /// <summary>
    /// 导航失败事件
    /// </summary>
    public event EventHandler<NavigationFailedEventArgs>? NavigationFailed;

    /// <summary>
    /// 是否启用导航动画
    /// </summary>
    public static readonly StyledProperty<bool> IsAnimationEnabledProperty =
        AvaloniaProperty.Register<LineFrame, bool>(nameof(IsAnimationEnabled), true);

    public bool IsAnimationEnabled
    {
        get => GetValue(IsAnimationEnabledProperty);
        set => SetValue(IsAnimationEnabledProperty, value);
    }

    /// <summary>
    /// 动画时长（毫秒）
    /// </summary>
    public static readonly StyledProperty<int> AnimationDurationProperty =
        AvaloniaProperty.Register<LineFrame, int>(nameof(AnimationDuration), 200);

    public int AnimationDuration
    {
        get => GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    /// <summary>
    /// 动画滑动方向
    /// </summary>
    public static readonly StyledProperty<SlideDirection> SlideDirectionProperty =
        AvaloniaProperty.Register<LineFrame, SlideDirection>(nameof(SlideDirection));

    public SlideDirection SlideDirection
    {
        get => GetValue(SlideDirectionProperty);
        set => SetValue(SlideDirectionProperty, value);
    }

    /// <summary>
    /// 导航到指定页面
    /// </summary>
    public bool Navigate(Type pageType, object? param = null)
    {
        if (_isNavigating)
        {
            s_logger.Warn("导航正在进行，已忽略重复导航");
            return false;
        }

        _isNavigating = true;
        _isNavigatingBack = false;
        try
        {
            var registration = PageRegistry.Instance.GetByType(pageType);
            if (registration == null)
            {
                s_logger.Error($"页面类型未注册: {pageType.FullName}");
                OnNavigationFailed(pageType, new InvalidOperationException($"Page type not registered: {pageType.FullName}"));
                return false;
            }

            return NavigateInternal(registration, param, recordHistory: true);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    /// 通过注册信息导航
    /// </summary>
    public bool Navigate(PageRegistration registration, object? param = null)
    {
        if (_isNavigating)
        {
            s_logger.Warn("导航正在进行，已忽略重复导航");
            return false;
        }

        _isNavigating = true;
        _isNavigatingBack = false;
        try
        {
            return NavigateInternal(registration, param, recordHistory: true);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    /// 导航到页面（不记录历史，用于返回操作）
    /// </summary>
    public bool NavigateWithoutHistory(PageRegistration registration, object? param = null)
    {
        if (_isNavigating)
        {
            s_logger.Warn("导航正在进行，已忽略重复导航");
            return false;
        }

        _isNavigating = true;
        _isNavigatingBack = true;
        try
        {
            return NavigateInternal(registration, param, recordHistory: false);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    /// 内部导航逻辑
    /// </summary>
    private bool NavigateInternal(PageRegistration registration, object? param, bool recordHistory)
    {
        try
        {
            // 保存当前页面到历史（仅在需要记录历史时）
            if (recordHistory && CurrentPage != null)
            {
                _historyStack.Push(new NavigationHistoryEntry(CurrentPage, registration, _currentParam));
            }

            // 根据存储模式获取或创建页面实例
            var newPage = PageRegistry.Instance.CreateInstance(registration, param);

            // 执行动画切换
            if (IsAnimationEnabled && AnimationDuration > 0)
            {
                PerformTransitionAnimation(newPage, registration, param);
            }
            else
            {
                // 直接设置内容
                CurrentPage = newPage;
                _currentParam = param;
                Content = newPage;
                s_logger.Debug($"导航到 {registration.PageType.Name}");
                OnNavigated(newPage, registration, param);
            }

            return true;
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, $"导航到 {registration.PageType.Name}");
            OnNavigationFailed(registration.PageType, ex);
            return false;
        }
    }

    /// <summary>
    /// 执行页面切换动画
    /// </summary>
    private async void PerformTransitionAnimation(PageBase newPage, PageRegistration registration, object? param)
    {
        // 设置新页面
        CurrentPage = newPage;
        _currentParam = param;
        Content = newPage;

        // 根据导航方向和设置确定滑动方向
        var slideDir = GetSlideDirection();
        var (offsetX, offsetY) = GetOffset(slideDir);

        // 设置初始状态
        newPage.Opacity = 0;

        // 设置初始变换（使用 TranslateTransform 直接设置）
        newPage.RenderTransform = new TranslateTransform(offsetX, offsetY);

        try
        {
            // 创建综合动画（透明度 + 位移）
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(AnimationDuration),
                PlaybackDirection = PlaybackDirection.Normal,
                IterationCount = new IterationCount(1),
                FillMode = FillMode.Forward,
                // 使用曲线缓动 - 慢启动快结束
                Easing = new QuadraticEaseIn()
            };

            // 起始关键帧：透明 + 偏移
            var startKeyframe = new KeyFrame
            {
                Cue = new Cue(0)
            };
            startKeyframe.Setters.Add(new Setter(OpacityProperty, 0.0));
            startKeyframe.Setters.Add(new Setter(TranslateTransform.XProperty, offsetX));
            startKeyframe.Setters.Add(new Setter(TranslateTransform.YProperty, offsetY));
            animation.Children.Add(startKeyframe);

            // 结束关键帧：完全不透明 + 回到原位
            var endKeyframe = new KeyFrame
            {
                Cue = new Cue(1)
            };
            endKeyframe.Setters.Add(new Setter(OpacityProperty, 1.0));
            endKeyframe.Setters.Add(new Setter(TranslateTransform.XProperty, 0.0));
            endKeyframe.Setters.Add(new Setter(TranslateTransform.YProperty, 0.0));
            animation.Children.Add(endKeyframe);

            // 执行动画
            await animation.RunAsync(newPage);

            // 动画完成后清理变换
            newPage.RenderTransform = null;

            s_logger.Debug($"导航到 {registration.PageType.Name} (动画完成)");
            OnNavigated(newPage, registration, param);
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "动画执行失败");
            newPage.Opacity = 1;
            newPage.RenderTransform = null;
            OnNavigated(newPage, registration, param);
        }
    }

    /// <summary>
    /// 获取实际的滑动方向
    /// </summary>
    private SlideDirection GetSlideDirection()
    {
        // 如果是返回导航，直接使用设置的方向（已经设置了正确的方向）
        if (_isNavigatingBack)
        {
            return SlideDirection;
        }

        // 前进导航时，Bottom/Top 方向反转（进入用 Bottom，返回用 Top）
        return SlideDirection switch
        {
            SlideDirection.Bottom => SlideDirection.Top,
            SlideDirection.Top => SlideDirection.Bottom,
            _ => SlideDirection
        };
    }

    /// <summary>
    /// 根据滑动方向获取偏移量（小幅度的平滑过渡）
    /// </summary>
    private static (double offsetX, double offsetY) GetOffset(SlideDirection direction)
    {
        return direction switch
        {
            SlideDirection.Right => (15, 0),
            SlideDirection.Left => (-15, 0),
            SlideDirection.Bottom => (0, 15),
            SlideDirection.Top => (0, -15),
            _ => (15, 0)
        };
    }

    /// <summary>
    /// 返回上一页
    /// </summary>
    public bool GoBack()
    {
        if (_historyStack.Count == 0)
        {
            s_logger.Debug("无法返回（历史为空）");
            return false;
        }

        var entry = _historyStack.Pop();

        // 对于 Singleton 页面，更新其参数
        if (entry.Registration.StorageMode == PageStorageMode.Singleton)
        {
            CurrentPage = entry.Page;
            entry.Page.ProcessParameter(entry.Param);
            _currentParam = entry.Param;
            Content = entry.Page;
        }
        else
        {
            // 对于其他模式，重新创建实例
            var newPage = PageRegistry.Instance.CreateInstance(entry.Registration, entry.Param);
            CurrentPage = newPage;
            _currentParam = entry.Param;
            Content = newPage;
        }

        s_logger.Debug($"返回至: {entry.Registration.PageType.Name}");
        OnNavigated(CurrentPage, entry.Registration, entry.Param);
        return true;
    }

    /// <summary>
    /// 是否可以返回
    /// </summary>
    public bool CanGoBack => _historyStack.Count > 0;

    /// <summary>
    /// 获取历史记录数量
    /// </summary>
    public int HistoryCount => _historyStack.Count;

    /// <summary>
    /// 清除历史记录
    /// </summary>
    public void ClearHistory()
    {
        _historyStack.Clear();
        s_logger.Debug("历史记录已清除");
    }

    /// <summary>
    /// 设置内容（不记录历史）
    /// </summary>
    public void SetContent(PageBase page)
    {
        CurrentPage = page;
        _currentParam = null;
        Content = page;
        s_logger.Debug($"直接设置页面: {page.GetType().Name}");
    }

    /// <summary>
    /// 触发导航完成事件
    /// </summary>
    protected virtual void OnNavigated(PageBase page, PageRegistration registration, object? param)
    {
        Navigated?.Invoke(this, new NavigationEventArgs(page, registration, param));
    }

    /// <summary>
    /// 触发导航失败事件
    /// </summary>
    protected virtual void OnNavigationFailed(Type pageType, Exception exception)
    {
        NavigationFailed?.Invoke(this, new NavigationFailedEventArgs(pageType, exception));
    }
}

/// <summary>
/// 导航历史记录条目
/// </summary>
public class NavigationHistoryEntry
{
    public PageBase Page { get; }
    public PageRegistration Registration { get; }
    public object? Param { get; }

    public NavigationHistoryEntry(PageBase page, PageRegistration registration, object? param)
    {
        Page = page;
        Registration = registration;
        Param = param;
    }
}

/// <summary>
/// 导航事件参数
/// </summary>
public class NavigationEventArgs : EventArgs
{
    public PageBase Page { get; }
    public PageRegistration Registration { get; }
    public object? Param { get; }

    public NavigationEventArgs(PageBase page, PageRegistration registration, object? param)
    {
        Page = page;
        Registration = registration;
        Param = param;
    }
}

/// <summary>
/// 导航失败事件参数
/// </summary>
public class NavigationFailedEventArgs : EventArgs
{
    public Type PageType { get; }
    public Exception Exception { get; }

    public NavigationFailedEventArgs(Type pageType, Exception exception)
    {
        PageType = pageType;
        Exception = exception;
    }
}
