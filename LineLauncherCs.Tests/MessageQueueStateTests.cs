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
using LMCUI.Controls;

namespace LineLauncherCs.Tests;

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
