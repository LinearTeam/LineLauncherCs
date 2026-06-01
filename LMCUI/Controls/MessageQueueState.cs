using System;
using System.Collections.Generic;
using System.Linq;

namespace LMCUI.Controls;

internal enum MessageQueueItemKind
{
    InfoBar,
    TeachingTip
}

internal sealed record MessageQueueItemState(
    string Id,
    MessageQueueItemKind Kind,
    int Duration);

internal sealed class MessageQueueState
{
    private readonly Queue<MessageQueueItemState> _pending = new();
    private readonly Dictionary<string, MessageQueueItemState> _active = new();

    public void Enqueue(MessageQueueItemState message)
    {
        _pending.Enqueue(message);
    }

    public IReadOnlyList<MessageQueueItemState> DequeueDisplayable()
    {
        var activated = new List<MessageQueueItemState>();
        var retained = new Queue<MessageQueueItemState>();
        var infoBarSlots = Math.Max(0, 3 - _active.Values.Count(message => message.Kind == MessageQueueItemKind.InfoBar));
        var teachingTipSlots = Math.Max(0, 1 - _active.Values.Count(message => message.Kind == MessageQueueItemKind.TeachingTip));

        while (_pending.Count > 0)
        {
            var message = _pending.Dequeue();
            var canActivate = message.Kind switch
            {
                MessageQueueItemKind.InfoBar => infoBarSlots > 0,
                MessageQueueItemKind.TeachingTip => teachingTipSlots > 0,
                _ => false
            };

            if (!canActivate)
            {
                retained.Enqueue(message);
                continue;
            }

            activated.Add(message);
            _active[message.Id] = message;
            if (message.Kind == MessageQueueItemKind.InfoBar)
            {
                infoBarSlots--;
            }
            else
            {
                teachingTipSlots--;
            }
        }

        while (retained.Count > 0)
        {
            _pending.Enqueue(retained.Dequeue());
        }

        return activated;
    }

    public bool Remove(string messageId)
    {
        if (_active.Remove(messageId))
        {
            return true;
        }

        var removed = false;
        var retained = new Queue<MessageQueueItemState>();
        while (_pending.Count > 0)
        {
            var message = _pending.Dequeue();
            if (!removed && message.Id == messageId)
            {
                removed = true;
                continue;
            }

            retained.Enqueue(message);
        }

        while (retained.Count > 0)
        {
            _pending.Enqueue(retained.Dequeue());
        }

        return removed;
    }

    public bool IsActive(string messageId)
    {
        return _active.ContainsKey(messageId);
    }
}
