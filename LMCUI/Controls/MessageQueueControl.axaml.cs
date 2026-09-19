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
using Avalonia.Styling;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;

namespace LMCUI.Controls;

public partial class MessageQueueControl : UserControl
{
    private readonly static Logger s_logger = new Logger("MessageQueueControl");
    private readonly MessageQueueState _queueState = new();
    private readonly Dictionary<string, IMessageItem> _messages = [];
    private readonly Dictionary<string, DispatcherTimer> _messageTimers = [];
    private readonly HashSet<string> _removingMessages = [];
    private bool _isProcessing;

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
            infoBar.Closing += (_, args) =>
            {
                if (_removingMessages.Contains(messageId))
                {
                    return;
                }

                args.Cancel = true;
                RemoveMessage(messageId);
            };
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
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RemoveMessage(messageId));
            return;
        }

        StopMessageTimer(messageId);

        if (!_messages.TryGetValue(messageId, out var message)
            || _removingMessages.Contains(messageId))
        {
            return;
        }

        if (!_queueState.IsActive(messageId))
        {
            if (_queueState.Remove(messageId))
            {
                _messages.Remove(messageId);
                ProcessQueue();
            }

            return;
        }

        _removingMessages.Add(messageId);
        if (!MessagePanel.Children.Contains(message.Control))
        {
            CompleteMessageRemoval(messageId, message);
            return;
        }

        StartMessageRemoval(messageId, message);
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
                ScheduleMessageExpiration(message);
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
                if (sender.Tag is not string tag)
                {
                    return;
                }

                if (!_removingMessages.Contains(tag))
                {
                    teachingTipItem.Control.IsOpen = true;
                }

                RemoveMessage(tag);
            };
            MessagePanel.Children.Add(teachingTipItem.Control);
            CreateFadeInAnimation(teachingTipItem.Control);
        }
    }

    private void CreateFadeInAnimation(Control control)
    {
        _ = CreateOpacityAnimation(
            control,
            0,
            1,
            TimeSpan.FromMilliseconds(220),
            new CubicEaseOut()).RunAsync(control);
    }

    private async void StartMessageRemoval(string messageId, IMessageItem message)
    {
        try
        {
            await CreateOpacityAnimation(
                message.Control,
                message.Control.Opacity,
                0,
                TimeSpan.FromMilliseconds(180),
                new CubicEaseIn()).RunAsync(message.Control);
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, $"Animating message removal: {messageId}");
        }

        CompleteMessageRemoval(messageId, message);
    }

    private void CompleteMessageRemoval(string messageId, IMessageItem message)
    {
        if (message.Control is FAInfoBar infoBar)
        {
            infoBar.IsOpen = false;
        }
        else if (message.Control is FATeachingTip teachingTip)
        {
            teachingTip.IsOpen = false;
            teachingTip.IsVisible = false;
            teachingTip.IsEnabled = false;
        }

        MessagePanel.Children.Remove(message.Control);
        _queueState.Remove(messageId);
        _messages.Remove(messageId);
        _removingMessages.Remove(messageId);
        ProcessQueue();
    }

    private void ScheduleMessageExpiration(IMessageItem message)
    {
        if (message.Duration <= 0)
        {
            RemoveMessage(message.Id);
            return;
        }

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(message.Duration)
        };
        void OnTimerTick(object? sender, EventArgs e)
        {
            timer.Stop();
            timer.Tick -= OnTimerTick;
            _messageTimers.Remove(message.Id);
            RemoveMessage(message.Id);
        }

        timer.Tick += OnTimerTick;
        _messageTimers[message.Id] = timer;
        timer.Start();
    }

    private void StopMessageTimer(string messageId)
    {
        if (_messageTimers.Remove(messageId, out var timer))
        {
            timer.Stop();
        }
    }

    private static Animation CreateOpacityAnimation(
        Control control,
        double from,
        double to,
        TimeSpan duration,
        Easing easing)
    {
        control.Opacity = from;
        var animation = new Animation
        {
            Duration = duration,
            FillMode = FillMode.Forward,
            Easing = easing
        };
        animation.Children.Add(new KeyFrame
        {
            Setters = { new Setter { Property = OpacityProperty, Value = from } },
            Cue = new Cue(0)
        });
        animation.Children.Add(new KeyFrame
        {
            Setters = { new Setter { Property = OpacityProperty, Value = to } },
            Cue = new Cue(1)
        });
        return animation;
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
