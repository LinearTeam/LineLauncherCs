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
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;

namespace LMCUI.Controls;

public partial class MessageQueueControl : UserControl
{
    private readonly static Logger s_logger = new Logger("MessageQueueControl");
    private readonly MessageQueueState _queueState = new();
    private readonly Dictionary<string, IMessageItem> _messages = new Dictionary<string, IMessageItem>();
    private readonly Dictionary<string, Timer> _messageTimers = new Dictionary<string, Timer>();
    private bool _isProcessing;
    private readonly SemaphoreSlim _animationLock = new SemaphoreSlim(1, 1);

    public static MessageQueueControl Instance { get; private set; } = null!;

    public MessageQueueControl()
    {
        Instance = this;
        InitializeComponent();
    }

    public string AddInfoBar(string title, string content, FAInfoBarSeverity severity = FAInfoBarSeverity.Informational, 
                            int duration = 5000, bool isClosable = true)
    {
        s_logger.Info($"显示InfoBar: {title} - {content}");
        string messageId = Guid.NewGuid().ToString();
        var infoBar = new FAInfoBar
        {
            Title = title,
            Message = content,
            Severity = severity,
            IsClosable = isClosable,
            IsOpen = true,
            Tag = messageId,
            IsIconVisible = true,
            MinWidth = 550,
            Opacity = 0 // 初始设置为透明
        };

        if (isClosable)
        {
            infoBar.Closed += (_, _) => RemoveMessage(messageId);
        }

        var messageItem = new InfoBarMessageItem
        {
            Id = messageId,
            Duration = duration,
            Control = infoBar
        };

        _messages[messageId] = messageItem;
        _queueState.Enqueue(new MessageQueueItemState(messageId, MessageQueueItemKind.InfoBar, duration));
        ProcessQueue();

        return messageId;
    }

    public string AddTeachingTip(FATeachingTip teachingTip, int duration = 5000)
    {
        string messageId = Guid.NewGuid().ToString();
        teachingTip.Tag = messageId;

        var messageItem = new TeachingTipMessageItem
        {
            Id = messageId,
            Duration = duration,
            Control = teachingTip
        };

        _messages[messageId] = messageItem;
        _queueState.Enqueue(new MessageQueueItemState(messageId, MessageQueueItemKind.TeachingTip, duration));
        ProcessQueue();

        return messageId;
    }
    
    public string AddTeachingTip(string title, string content, int duration = 5000, 
                               FATeachingTipPlacementMode placement = FATeachingTipPlacementMode.Bottom)
    {
        string messageId = Guid.NewGuid().ToString();
        var teachingTip = new FATeachingTip
        {
            Title = title,
            Content = content,
            IsOpen = false,
            IsVisible = false,
            IsEnabled = false,
            Tag = messageId,
            Opacity = 0, // 初始设置为透明
            // PreferredPlacement = placement
        };

        var messageItem = new TeachingTipMessageItem
        {
            Id = messageId,
            Duration = duration,
            Control = teachingTip
        };

        _messages[messageId] = messageItem;
        _queueState.Enqueue(new MessageQueueItemState(messageId, MessageQueueItemKind.TeachingTip, duration));
        ProcessQueue();

        return messageId;
    }

    public void RemoveMessage(string messageId)
    {
        if (_messageTimers.TryGetValue(messageId, out var timer))
        {
            timer.Dispose();
            _messageTimers.Remove(messageId);
        }

        if (_queueState.Remove(messageId))
        {
            RemoveMessageFromUi(messageId);
        }

        _messages.Remove(messageId);
        ProcessQueue();
    }

    private void ProcessQueue()
    {
        if (_isProcessing)
            return;

        _isProcessing = true;

        foreach (var messageState in _queueState.DequeueDisplayable())
        {
            if (_messages.TryGetValue(messageState.Id, out var message))
            {
                AddMessageToUi(message);
                _messageTimers[messageState.Id] = new Timer(_ =>
                {
                    Dispatcher.UIThread.Post(() => RemoveMessage(messageState.Id));
                }, null, messageState.Duration, Timeout.Infinite);
            }
        }

        _isProcessing = false;
    }

    private void AddMessageToUi(IMessageItem message)
    {
        if (message is InfoBarMessageItem infoBarItem)
        {
            MessagePanel.Children.Add(infoBarItem.Control);
            CreateFadeInAnimation(infoBarItem.Control);
        }
        else if (message is TeachingTipMessageItem teachingTipItem)
        {
            teachingTipItem.Control.IsEnabled = true;
            teachingTipItem.Control.IsVisible = true;
            teachingTipItem.Control.IsOpen = true;
            teachingTipItem.Control.Closed += (sender, _) =>
            {
                if (sender.Tag is string tag) RemoveMessage(tag);
            };
            MessagePanel.Children.Add(teachingTipItem.Control);
            CreateFadeInAnimation(teachingTipItem.Control);
        }
    }

    private void CreateFadeInAnimation(Control control)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(500),
            FillMode = FillMode.Forward,
            Easing = new CubicEaseOut()
        };

        animation.Children.Add(new KeyFrame
        {
            Setters = {
                new Setter{ Property = OpacityProperty, Value = 0.0d }
            },
            Cue = new Cue(0.0d)
        });

        animation.Children.Add(new KeyFrame
        {
            Setters = {
                new Setter { Property = OpacityProperty, Value = 1.0d }
            },
            Cue = new Cue(1.0d)
        });

        animation.RunAsync(control);
    }

    async private void CreateFadeOutAndRemoveAnimation(Control control)
    {
        await _animationLock.WaitAsync();
        try
        {
            var fadeAnimation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(500),
                FillMode = FillMode.Forward,
                Easing = new CubicEaseInOut()
            };
            
            fadeAnimation.Children.Add(new KeyFrame
            {
                Setters = {
                    new Setter { Property = OpacityProperty, Value = 1.0d }
                },
                Cue = new Cue(0.0d)
            });
            
            fadeAnimation.Children.Add(new KeyFrame
            {
                Setters = {
                    new Setter { Property = OpacityProperty, Value = 0.0d }
                },
                Cue = new Cue(1.0d)
            });

            var slideAnimation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(500),
                FillMode = FillMode.Forward,
                Easing = new CubicEaseInOut()
            };

            slideAnimation.Children.Add(new KeyFrame
            {
                Setters = {
                    new Setter { Property = MarginProperty, Value = new Thickness(0) }
                },
                Cue = new Cue(0.0d)
            });

            slideAnimation.Children.Add(new KeyFrame
            {
                Setters = {
                    new Setter { Property = MarginProperty, Value = new Thickness(0, -control.Bounds.Height, 0, 0) }
                },
                Cue = new Cue(1.0d)
            });

            // 并行运行动画
            var fadeTask = fadeAnimation.RunAsync(control);
            var slideTask = slideAnimation.RunAsync(control);
            await Task.WhenAll(fadeTask, slideTask);
            
            // 等待动画完成后再移除控件
            await Task.Delay(100);
            
            MessagePanel.Children.Remove(control);
            ProcessQueue();
        }
        finally
        {
            _animationLock.Release();
        }
    }

    private void RemoveMessageFromUi(string messageId)
    {
        for (int i = MessagePanel.Children.Count - 1; i >= 0; i--)
        {
            var child = MessagePanel.Children[i];
            if (child is FAInfoBar infoBar && infoBar.Tag as string == messageId)
            {
                infoBar.IsOpen = false;
                CreateFadeOutAndRemoveAnimation(infoBar);
                return;
            }
            else if (child is FATeachingTip teachingTip && teachingTip.Tag as string == messageId)
            {
                teachingTip.IsOpen = false;
                CreateFadeOutAndRemoveAnimation(teachingTip);
                return;
            }
        }
    }
}

public interface IMessageItem
{
    string Id { get; set; }
    int Duration { get; set; }
    Control Control { get; set; }
}

public class InfoBarMessageItem : IMessageItem
{
    public string Id { get; set; } = string.Empty;
    public int Duration { get; set; } = 5000;
    public FAInfoBar Control { get; set; } = null!;
    Control IMessageItem.Control 
    { 
        get => Control;
        set => Control = (FAInfoBar)value;
    }
}

public class TeachingTipMessageItem : IMessageItem
{
    public string Id { get; set; } = string.Empty;
    public int Duration { get; set; } = 5000;
    public FATeachingTip Control { get; set; } = null!;
    Control IMessageItem.Control 
    { 
        get => Control;
        set => Control = (FATeachingTip)value;
    }
}
