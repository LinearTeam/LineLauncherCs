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

using Avalonia.Controls;
using Avalonia.Markup.Xaml.Templates;
using FluentAvalonia.UI.Controls;
using LMCUI.Behaviors;

namespace LineLauncherCs.Tests;

public class HoverFooterBehaviorTests
{
    [Fact]
    public Task Hover_ReusesMeasuredFooterWithoutCollapsingIt() => TestAppBuilder.RunOnUiThreadAsync(() =>
    {
        foreach (var owner in new Control[] { new FASettingsExpander(), new FASettingsExpanderItem() })
        {
            HoverFooterBehavior.SetFooterContentTemplate(owner, new ControlTemplate());
            var footer = Assert.IsType<ContentControl>(GetFooter(owner));
            Assert.True(footer.IsVisible);
            Assert.Equal(0, footer.Opacity);
            Assert.False(footer.IsHitTestVisible);

            HoverFooterBehavior.UpdateFooterVisibility(owner, true);
            Assert.Same(footer, GetFooter(owner));
            Assert.Equal(1, footer.Opacity);
            Assert.True(footer.IsHitTestVisible);

            HoverFooterBehavior.UpdateFooterVisibility(owner, false);
            Assert.Same(footer, GetFooter(owner));
            Assert.True(footer.IsVisible);
            Assert.Equal(0, footer.Opacity);
            Assert.False(footer.IsHitTestVisible);

            HoverFooterBehavior.SetFooterContentTemplate(owner, new ControlTemplate());
            Assert.NotSame(footer, GetFooter(owner));
            HoverFooterBehavior.SetFooterContentTemplate(owner, null);
            Assert.Null(GetFooter(owner));
        }
    });

    private static object? GetFooter(Control control) => control switch
    {
        FASettingsExpander expander => expander.Footer,
        FASettingsExpanderItem item => item.Footer,
        _ => null
    };
}
