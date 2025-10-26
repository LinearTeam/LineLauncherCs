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

namespace LMCUI.Controls;

public partial class MessageQueueControl : UserControl
{
    private readonly Queue<IMessageItem> _messageQueue = new Queue<IMessageItem>();
    private readonly Dictionary<string, Timer> _messageTimers = new Dictionary<string, Timer>();
    private bool _isProcessing;
    private int _currentInfoBarCount;
    private int _currentTeachingTipCount;
    private readonly SemaphoreSlim _animationLock = new SemaphoreSlim(1, 1);

    public static MessageQueueControl Instance { get; private set; } = null!;

    public MessageQueueControl()
    {
        Instance = this;
        InitializeComponent();
    }

    public string AddInfoBar(string title, string content, InfoBarSeverity severity = InfoBarSeverity.Informational, 
                            int duration = 5000, bool isClosable = true)
    {
        string messageId = Guid.NewGuid().ToString();
        var infoBar = new InfoBar
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
            infoBar.Closed += (sender, e) => RemoveMessage(messageId);
        }

        var messageItem = new InfoBarMessageItem
        {
            Id = messageId,
            Duration = duration,
            Control = infoBar
        };

        _messageQueue.Enqueue(messageItem);
        ProcessQueue();

        return messageId;
    }

    public string AddTeachingTip(TeachingTip teachingTip, int duration = 5000)
    {
        string messageId = Guid.NewGuid().ToString();
        teachingTip.Tag = messageId;

        var messageItem = new TeachingTipMessageItem
        {
            Id = messageId,
            Duration = duration,
            Control = teachingTip
        };

        _messageQueue.Enqueue(messageItem);
        ProcessQueue();

        return messageId;
    }
    
    public string AddTeachingTip(string title, string content, int duration = 5000, 
                               TeachingTipPlacementMode placement = TeachingTipPlacementMode.Bottom)
    {
        string messageId = Guid.NewGuid().ToString();
        var teachingTip = new TeachingTip
        {
            Title = title,
            Content = content,
            IsOpen = false,
            IsVisible = false,
            IsEnabled = false,
            Tag = messageId,
            Opacity = 0 // 初始设置为透明
            // PreferredPlacement = placement
        };

        var messageItem = new TeachingTipMessageItem
        {
            Id = messageId,
            Duration = duration,
            Control = teachingTip
        };

        _messageQueue.Enqueue(messageItem);
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

        RemoveMessageFromUi(messageId);
    }

    private void ProcessQueue()
    {
        if (_isProcessing || _messageQueue.Count == 0)
            return;

        _isProcessing = true;

        // 分开处理两种消息类型的限制
        ProcessTeachingTips();
        ProcessInfoBars();

        _isProcessing = false;
    }

    private void ProcessTeachingTips()
    {
        while (_messageQueue.Count > 0 && _messageQueue.Peek() is TeachingTipMessageItem)
        {
            if (_currentTeachingTipCount >= 1)
                break;

            var message = _messageQueue.Dequeue();
            AddMessageToUi(message);

            var timer = new Timer(state => 
            {
                Dispatcher.UIThread.Post(() => RemoveMessage(message.Id));
            }, null, message.Duration, Timeout.Infinite);

            _messageTimers[message.Id] = timer;
        }
    }

    private void ProcessInfoBars()
    {
        while (_messageQueue.Count > 0 && _messageQueue.Peek() is InfoBarMessageItem)
        {
            if (_currentInfoBarCount >= 3)
                break;

            var message = _messageQueue.Dequeue();
            AddMessageToUi(message);

            var timer = new Timer(state => 
            {
                Dispatcher.UIThread.Post(() => RemoveMessage(message.Id));
            }, null, message.Duration, Timeout.Infinite);

            _messageTimers[message.Id] = timer;
        }
    }

    private void AddMessageToUi(IMessageItem message)
    {
        if (message is InfoBarMessageItem infoBarItem)
        {
            MessagePanel.Children.Add(infoBarItem.Control);
            _currentInfoBarCount++;
            CreateFadeInAnimation(infoBarItem.Control);
        }
        else if (message is TeachingTipMessageItem teachingTipItem)
        {
            teachingTipItem.Control.IsEnabled = true;
            teachingTipItem.Control.IsVisible = true;
            teachingTipItem.Control.IsOpen = true;
            teachingTipItem.Control.Closed += (sender, args) =>
            {
                if (sender.Tag is string tag) RemoveMessage(tag);
            };
            MessagePanel.Children.Add(teachingTipItem.Control);
            _currentTeachingTipCount++;
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
            
            if (control is InfoBar)
                _currentInfoBarCount--;
            else if (control is TeachingTip)
                _currentTeachingTipCount--;
            
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
            if (child is InfoBar infoBar && infoBar.Tag as string == messageId)
            {
                infoBar.IsOpen = false;
                CreateFadeOutAndRemoveAnimation(infoBar);
                return;
            }
            else if (child is TeachingTip teachingTip && teachingTip.Tag as string == messageId)
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
    public InfoBar Control { get; set; } = null!;
    Control IMessageItem.Control 
    { 
        get => Control;
        set => Control = (InfoBar)value;
    }
}

public class TeachingTipMessageItem : IMessageItem
{
    public string Id { get; set; } = string.Empty;
    public int Duration { get; set; } = 5000;
    public TeachingTip Control { get; set; } = null!;
    Control IMessageItem.Control 
    { 
        get => Control;
        set => Control = (TeachingTip)value;
    }
}