using LMCUI.Controls;

namespace LMC.Tests;

public class MessageQueueStateTests
{
    [Fact]
    public void DequeueDisplayable_RespectsTypeCapacities()
    {
        var state = new MessageQueueState();
        state.Enqueue(new MessageQueueItemState("i1", MessageQueueItemKind.InfoBar, 1000));
        state.Enqueue(new MessageQueueItemState("i2", MessageQueueItemKind.InfoBar, 1000));
        state.Enqueue(new MessageQueueItemState("t1", MessageQueueItemKind.TeachingTip, 1000));
        state.Enqueue(new MessageQueueItemState("i3", MessageQueueItemKind.InfoBar, 1000));
        state.Enqueue(new MessageQueueItemState("i4", MessageQueueItemKind.InfoBar, 1000));
        state.Enqueue(new MessageQueueItemState("t2", MessageQueueItemKind.TeachingTip, 1000));

        var activated = state.DequeueDisplayable();

        Assert.Equal(["i1", "i2", "t1", "i3"], activated.Select(item => item.Id).ToArray());
        Assert.True(state.IsActive("i1"));
        Assert.False(state.IsActive("i4"));
    }

    [Fact]
    public void Remove_RemovesPendingAndActiveMessages()
    {
        var state = new MessageQueueState();
        state.Enqueue(new MessageQueueItemState("i1", MessageQueueItemKind.InfoBar, 1000));
        state.Enqueue(new MessageQueueItemState("t1", MessageQueueItemKind.TeachingTip, 1000));
        _ = state.DequeueDisplayable();

        Assert.True(state.Remove("i1"));
        Assert.True(state.Remove("t1"));
        Assert.False(state.IsActive("i1"));
        Assert.False(state.IsActive("t1"));
    }

    [Fact]
    public void DequeueDisplayable_ActivatesQueuedMessageAfterRemoval()
    {
        var state = new MessageQueueState();
        state.Enqueue(new MessageQueueItemState("i1", MessageQueueItemKind.InfoBar, 1000));
        state.Enqueue(new MessageQueueItemState("i2", MessageQueueItemKind.InfoBar, 1000));
        state.Enqueue(new MessageQueueItemState("i3", MessageQueueItemKind.InfoBar, 1000));
        state.Enqueue(new MessageQueueItemState("i4", MessageQueueItemKind.InfoBar, 1000));

        _ = state.DequeueDisplayable();
        Assert.True(state.Remove("i1"));

        var activated = state.DequeueDisplayable();

        Assert.Single(activated);
        Assert.Equal("i4", activated[0].Id);
    }
}
