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
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace LMCUI.Behaviors;

public static class SmoothStackPanelChildrenBehavior
{
    private readonly static ConditionalWeakTable<StackPanel, StackPanelAnimator> s_animators = new();

    public readonly static AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<StackPanel, bool>(
            "IsEnabled",
            typeof(SmoothStackPanelChildrenBehavior));

    static SmoothStackPanelChildrenBehavior()
    {
        IsEnabledProperty.Changed.Subscribe(new EnabledObserver());
    }

    public static bool GetIsEnabled(StackPanel panel)
    {
        return panel.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(StackPanel panel, bool value)
    {
        panel.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Sender is not StackPanel panel)
        {
            return;
        }

        var isEnabled = args.NewValue is true;
        if (isEnabled)
        {
            s_animators.GetValue(panel, static key => new StackPanelAnimator(key));

            return;
        }

        RemoveAnimator(panel);
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

    private static void RemoveAnimator(StackPanel panel)
    {
        if (!s_animators.TryGetValue(panel, out var animator))
        {
            return;
        }

        s_animators.Remove(panel);
        animator.Dispose();
    }

    private sealed class StackPanelAnimator : IDisposable
    {
        private readonly static TimeSpan s_animationDuration = TimeSpan.FromMilliseconds(160);
        private readonly static TimeSpan s_layoutSettleDelay = TimeSpan.FromMilliseconds(50);
        private readonly StackPanel _panel;
        private readonly Dictionary<Control, Rect> _lastBounds = [];
        private readonly Dictionary<Control, Rect> _stableBounds = [];
        private readonly Dictionary<Control, TranslateTransform> _transforms = [];
        private readonly HashSet<Control> _pendingChildren = [];
        private readonly List<Control> _visibleChildren = [];
        private readonly HashSet<Control> _visibleChildSet = [];
        private readonly List<Control> _removedChildren = [];
        private readonly DispatcherTimer _layoutSettleTimer;
        private bool _hasInitialSnapshot;
        private bool _isDisposed;

        public StackPanelAnimator(StackPanel panel)
        {
            _panel = panel;
            _panel.LayoutUpdated += OnLayoutUpdated;
            _panel.AttachedToVisualTree += OnAttachedToVisualTree;
            _panel.DetachedFromVisualTree += OnDetachedFromVisualTree;
            _layoutSettleTimer = new DispatcherTimer
            {
                Interval = s_layoutSettleDelay
            };
            _layoutSettleTimer.Tick += LayoutSettleTimer_OnTick;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _panel.LayoutUpdated -= OnLayoutUpdated;
            _panel.AttachedToVisualTree -= OnAttachedToVisualTree;
            _panel.DetachedFromVisualTree -= OnDetachedFromVisualTree;
            _layoutSettleTimer.Stop();
            _layoutSettleTimer.Tick -= LayoutSettleTimer_OnTick;
            _lastBounds.Clear();
            _stableBounds.Clear();
            _transforms.Clear();
            _pendingChildren.Clear();
            _visibleChildren.Clear();
            _visibleChildSet.Clear();
            _removedChildren.Clear();
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            ResetSnapshot();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            ResetSnapshot();
        }

        private void ResetSnapshot()
        {
            foreach (var transform in _transforms.Values)
            {
                SetTransformImmediately(transform, 0, 0);
            }

            _lastBounds.Clear();
            _stableBounds.Clear();
            _pendingChildren.Clear();
            _layoutSettleTimer.Stop();
            _hasInitialSnapshot = false;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            if (_isDisposed || !_panel.IsEffectivelyVisible)
            {
                return;
            }

            AnalyzeLayout();
        }

        private void AnalyzeLayout()
        {
            if (_isDisposed || !_panel.IsEffectivelyVisible)
            {
                return;
            }

            _visibleChildren.Clear();
            _visibleChildSet.Clear();
            foreach (var child in _panel.Children)
            {
                if (child is Control control && control.IsVisible)
                {
                    _visibleChildren.Add(control);
                    _visibleChildSet.Add(control);
                }
            }

            if (!_hasInitialSnapshot)
            {
                SnapshotChildren(_visibleChildren);
                _hasInitialSnapshot = true;
                return;
            }

            CleanupRemovedChildren();
            var hasLayoutChanges = false;

            foreach (var child in _visibleChildren)
            {
                var currentBounds = child.Bounds;
                if (currentBounds.Width <= 0 && currentBounds.Height <= 0)
                {
                    _lastBounds[child] = currentBounds;
                    continue;
                }

                var hasCurrentBoundsChanged = !_lastBounds.TryGetValue(child, out var previousBounds)
                                              || HasBoundsChanged(currentBounds, previousBounds);

                if (!_stableBounds.TryGetValue(child, out var stableBounds))
                {
                    _stableBounds[child] = currentBounds;
                    _lastBounds[child] = currentBounds;
                    continue;
                }

                if (hasCurrentBoundsChanged)
                {
                    hasLayoutChanges = true;
                }

                if (HasBoundsChanged(currentBounds, stableBounds))
                {
                    _pendingChildren.Add(child);
                    HoldChildAtStablePosition(child, currentBounds, stableBounds);
                }

                _lastBounds[child] = currentBounds;
            }

            if (hasLayoutChanges || _pendingChildren.Count > 0)
            {
                _layoutSettleTimer.Stop();
                _layoutSettleTimer.Start();
            }
        }

        private void SnapshotChildren(IEnumerable<Control> children)
        {
            _lastBounds.Clear();
            _stableBounds.Clear();
            foreach (var child in children)
            {
                var bounds = child.Bounds;
                _lastBounds[child] = bounds;
                _stableBounds[child] = bounds;
            }
        }

        private void CleanupRemovedChildren()
        {
            _removedChildren.Clear();
            foreach (var child in _lastBounds.Keys)
            {
                if (!_visibleChildSet.Contains(child))
                {
                    _removedChildren.Add(child);
                }
            }

            foreach (var child in _removedChildren)
            {
                _lastBounds.Remove(child);
                _stableBounds.Remove(child);
                _pendingChildren.Remove(child);
                _transforms.Remove(child);
            }
        }

        private void HoldChildAtStablePosition(Control child, Rect currentBounds, Rect stableBounds)
        {
            var transform = GetOrCreateTranslateTransform(child);
            SetTransformImmediately(
                transform,
                stableBounds.X - currentBounds.X,
                stableBounds.Y - currentBounds.Y);
        }

        private void LayoutSettleTimer_OnTick(object? sender, EventArgs e)
        {
            _layoutSettleTimer.Stop();
            if (_isDisposed)
            {
                return;
            }

            foreach (var child in _pendingChildren.ToArray())
            {
                if (!_visibleChildSet.Contains(child))
                {
                    continue;
                }

                var currentBounds = child.Bounds;
                var stableBounds = _stableBounds[child];
                var transform = GetOrCreateTranslateTransform(child);
                var offsetX = stableBounds.X - currentBounds.X;
                var offsetY = stableBounds.Y - currentBounds.Y;
                SetTransformImmediately(transform, offsetX, offsetY);

                if (Math.Abs(offsetX) > 0.5 || Math.Abs(offsetY) > 0.5)
                {
                    transform.X = 0;
                    transform.Y = 0;
                }

                _stableBounds[child] = currentBounds;
                _lastBounds[child] = currentBounds;
            }

            _pendingChildren.Clear();
        }

        private static bool HasBoundsChanged(Rect first, Rect second)
        {
            return Math.Abs(first.X - second.X) > 0.5
                   || Math.Abs(first.Y - second.Y) > 0.5
                   || Math.Abs(first.Width - second.Width) > 0.5
                   || Math.Abs(first.Height - second.Height) > 0.5;
        }

        private static void SetTransformImmediately(
            TranslateTransform transform,
            double x,
            double y)
        {
            var transitions = transform.Transitions;
            transform.Transitions = null;
            transform.X = x;
            transform.Y = y;
            transform.Transitions = transitions;
        }

        private TranslateTransform GetOrCreateTranslateTransform(Control child)
        {
            if (_transforms.TryGetValue(child, out var transform))
            {
                return transform;
            }

            transform = child.RenderTransform switch
            {
                TransformGroup group => AppendTranslateTransform(group),
                Transform existingTransform => CreateTransformGroup(child, existingTransform),
                _ => new TranslateTransform()
            };

            if (child.RenderTransform == null)
            {
                child.RenderTransform = transform;
            }

            child.RenderTransformOrigin = RelativePoint.TopLeft;
            transform.Transitions ??=
            [
                new DoubleTransition
                {
                    Property = TranslateTransform.XProperty,
                    Duration = s_animationDuration,
                    Easing = new CubicEaseOut()
                },
                new DoubleTransition
                {
                    Property = TranslateTransform.YProperty,
                    Duration = s_animationDuration,
                    Easing = new CubicEaseOut()
                }
            ];

            _transforms[child] = transform;
            return transform;
        }

        private static TranslateTransform AppendTranslateTransform(TransformGroup group)
        {
            var transform = new TranslateTransform();
            group.Children.Add(transform);
            return transform;
        }

        private static TranslateTransform CreateTransformGroup(Control child, Transform existingTransform)
        {
            var transform = new TranslateTransform();
            child.RenderTransform = new TransformGroup
            {
                Children =
                {
                    existingTransform,
                    transform
                }
            };

            return transform;
        }
    }
}
