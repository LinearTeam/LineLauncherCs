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

namespace LMCUI.Behaviors;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using System;
using Avalonia.Markup.Xaml.Templates;

public static class HoverFooterBehavior
{
    public readonly static AttachedProperty<ControlTemplate> FooterContentTemplateProperty =
        AvaloniaProperty.RegisterAttached<Control, ControlTemplate>(
            "FooterContentTemplate", 
            typeof(HoverFooterBehavior));

    public static ControlTemplate GetFooterContentTemplate(Control obj) =>
        obj.GetValue(FooterContentTemplateProperty);

    public static void SetFooterContentTemplate(Control obj, ControlTemplate value) =>
        obj.SetValue(FooterContentTemplateProperty, value);

    private class BehaviorObserver : IObserver<AvaloniaPropertyChangedEventArgs>
    {
        public void OnNext(AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Sender is not Control ctrl) return;
            
            ctrl.PointerEntered -= OnPointerEntered;
            ctrl.PointerExited -= OnPointerExited;
            
            if (args.NewValue is ControlTemplate)
            {
                ctrl.PointerEntered += OnPointerEntered;
                ctrl.PointerExited += OnPointerExited;
            }
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }

    static HoverFooterBehavior()
    {
        FooterContentTemplateProperty.Changed.Subscribe(new BehaviorObserver());
    }

    private static void OnPointerEntered(object? sender, RoutedEventArgs e)
    {
        if (sender is SettingsExpander expander)
            expander.Footer = CreateFooterContent(expander);
        else if (sender is SettingsExpanderItem item)
            item.Footer = CreateFooterContent(item);
    }

    // 修复模板加载问题
    private static Control CreateFooterContent(Control owner)
    {
        var template = GetFooterContentTemplate(owner);

        // 正确加载模板
        var contentControl = new ContentControl
        {
            Template = template
        };
        return contentControl;
    }

    private static void OnPointerExited(object? sender, RoutedEventArgs e)
    {
        if (sender is SettingsExpander expander)
            expander.Footer = null;
        else if (sender is SettingsExpanderItem item)
            item.Footer = null;
    }
}