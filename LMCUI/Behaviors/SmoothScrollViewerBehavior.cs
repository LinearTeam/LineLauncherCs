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
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace LMCUI.Behaviors;

/// <summary>
/// Converts mouse-wheel scrolling into animated <see cref="ScrollViewer.Offset"/> changes.
/// </summary>
public static class SmoothScrollViewerBehavior
{
    private readonly static ConditionalWeakTable<ScrollViewer, ScrollAnimator> s_animators = new();

    public readonly static AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "IsEnabled",
            typeof(SmoothScrollViewerBehavior));

    static SmoothScrollViewerBehavior()
    {
        IsEnabledProperty.Changed.Subscribe(new EnabledObserver());
    }

    public static bool GetIsEnabled(ScrollViewer scrollViewer)
    {
        return scrollViewer.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(ScrollViewer scrollViewer, bool value)
    {
        scrollViewer.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (args.NewValue is true)
        {
            s_animators.GetValue(scrollViewer, static key => new ScrollAnimator(key));
            return;
        }

        if (s_animators.TryGetValue(scrollViewer, out var animator))
        {
            s_animators.Remove(scrollViewer);
            animator.Dispose();
        }
    }

    private sealed class EnabledObserver : IObserver<AvaloniaPropertyChangedEventArgs<bool>>
    {
        public void OnNext(AvaloniaPropertyChangedEventArgs<bool> value)
        {
            OnIsEnabledChanged(value);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }

    private sealed class ScrollAnimator : IDisposable
    {
        private const double WheelScrollAmount = 50;
        private readonly static TimeSpan s_animationDuration = TimeSpan.FromMilliseconds(160);
        private readonly static TimeSpan s_hoverResumeDelay = TimeSpan.FromMilliseconds(100);
        private readonly ScrollViewer _scrollViewer;
        private readonly DispatcherTimer _animationTimer;
        private readonly DispatcherTimer _hoverResumeTimer;
        private Vector _animationStartOffset;
        private Vector _targetOffset;
        private DateTime _animationStartedAt;
        private bool _isDisposed;

        public ScrollAnimator(ScrollViewer scrollViewer)
        {
            _scrollViewer = scrollViewer;
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += OnAnimationTick;
            _hoverResumeTimer = new DispatcherTimer { Interval = s_hoverResumeDelay };
            _hoverResumeTimer.Tick += OnHoverResumeTimerTick;
            _scrollViewer.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnPointerWheelChanged,
                RoutingStrategies.Tunnel);
            _scrollViewer.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _animationTimer.Stop();
            _animationTimer.Tick -= OnAnimationTick;
            _hoverResumeTimer.Stop();
            _hoverResumeTimer.Tick -= OnHoverResumeTimerTick;
            SetScrollInteractionActive(false);
            _scrollViewer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            _scrollViewer.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_isDisposed || e.Source is not Visual source || source.FindAncestorOfType<ScrollViewer>() != _scrollViewer)
            {
                return;
            }

            var delta = e.Delta;
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && NearlyZero(delta.X))
            {
                delta = new Vector(delta.Y, 0);
            }

            var currentOffset = _scrollViewer.Offset;
            var baseOffset = _animationTimer.IsEnabled ? _targetOffset : currentOffset;
            var maxOffset = new Vector(
                Math.Max(0, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width),
                Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height));
            var targetOffset = new Vector(
                Math.Clamp(baseOffset.X - delta.X * WheelScrollAmount, 0, maxOffset.X),
                Math.Clamp(baseOffset.Y - delta.Y * WheelScrollAmount, 0, maxOffset.Y));

            if (targetOffset == baseOffset)
            {
                return;
            }

            _targetOffset = targetOffset;
            _animationStartOffset = currentOffset;
            _animationStartedAt = DateTime.UtcNow;
            _hoverResumeTimer.Stop();
            SetScrollInteractionActive(true);
            _animationTimer.Start();
            e.Handled = true;
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            var progress = Math.Clamp(
                (DateTime.UtcNow - _animationStartedAt).TotalMilliseconds / s_animationDuration.TotalMilliseconds,
                0,
                1);
            var easedProgress = 1 - Math.Pow(1 - progress, 3);
            var offset = new Vector(
                _animationStartOffset.X + (_targetOffset.X - _animationStartOffset.X) * easedProgress,
                _animationStartOffset.Y + (_targetOffset.Y - _animationStartOffset.Y) * easedProgress);

            _scrollViewer.SetCurrentValue(ScrollViewer.OffsetProperty, offset);

            if (progress >= 1)
            {
                _animationTimer.Stop();
                _hoverResumeTimer.Start();
            }
        }

        private void OnHoverResumeTimerTick(object? sender, EventArgs e)
        {
            _hoverResumeTimer.Stop();
            SetScrollInteractionActive(false);
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _animationTimer.Stop();
            _hoverResumeTimer.Stop();
            SetScrollInteractionActive(false);
        }

        private void SetScrollInteractionActive(bool value)
        {
            _scrollViewer.Classes.Set("smooth-scroll-active", value);
        }

        private static bool NearlyZero(double value)
        {
            return Math.Abs(value) < 0.0001;
        }
    }
}
