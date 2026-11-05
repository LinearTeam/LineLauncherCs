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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LMCUI.Behaviors;

namespace LineLauncherCs.Tests;

public class SmoothStackPanelChildrenBehaviorTests
{
    [Fact]
    public Task RapidLayoutReversal_RetargetsAndClearsHeldOffsets() => TestAppBuilder.RunOnUiThreadAsync(() =>
    {
        var expanding = new Border { Height = 60 };
        var following = new Border { Height = 30 };
        var panel = new StackPanel { Children = { expanding, following } };
        using var animator = new SmoothStackPanelChildrenBehavior.StackPanelAnimator(panel);
        Arrange();
        Assert.Equal(60, following.Bounds.Y);

        expanding.Height = 140;
        Arrange();
        Assert.Equal(140, following.Bounds.Y);
        var transform = Assert.IsType<TranslateTransform>(following.RenderTransform);
        Assert.Equal(-80, transform.Y);

        expanding.Height = 60;
        Arrange();
        Assert.Equal(0, transform.Y);

        // Simulate a transition halfway to its destination before a new layout arrives.
        transform.Transitions = null;
        transform.Y = -30;
        expanding.Height = 140;
        Arrange();
        Assert.Equal(-110, transform.Y);

        following.IsVisible = false;
        Arrange();
        Assert.Equal(0, transform.Y);

        following.IsVisible = true;
        Arrange();
        expanding.Height = 60;
        Arrange();
        animator.Dispose();
        Assert.Equal(0, following.RenderTransform!.Value.M32);

        void Arrange()
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
            panel.Measure(new Size(300, double.PositiveInfinity));
            panel.Arrange(new Rect(0, 0, 300, panel.DesiredSize.Height));
            animator.AnalyzeLayout();
        }
    });
}
