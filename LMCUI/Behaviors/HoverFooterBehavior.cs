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
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using System;
using Avalonia.Markup.Xaml.Templates;

public static class HoverFooterBehavior
{
    public readonly static AttachedProperty<ControlTemplate?> FooterContentTemplateProperty =
        AvaloniaProperty.RegisterAttached<Control, ControlTemplate?>(
            "FooterContentTemplate", 
            typeof(HoverFooterBehavior));

    private readonly static AttachedProperty<ContentControl?> FooterContentProperty =
        AvaloniaProperty.RegisterAttached<Control, ContentControl?>(
            "FooterContent", typeof(HoverFooterBehavior));

    public static ControlTemplate? GetFooterContentTemplate(Control obj) =>
        obj.GetValue(FooterContentTemplateProperty);

    public static void SetFooterContentTemplate(Control obj, ControlTemplate? value) =>
        obj.SetValue(FooterContentTemplateProperty, value);

    private class BehaviorObserver : IObserver<AvaloniaPropertyChangedEventArgs>
    {
        public void OnNext(AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Sender is not Control ctrl) return;
            
            ctrl.PointerEntered -= OnPointerEntered;
            ctrl.PointerExited -= OnPointerExited;
            ctrl.PropertyChanged -= OnOwnerPropertyChanged;

            if (ctrl.GetValue(FooterContentProperty) is { } previousFooter)
            {
                if (ctrl is FASettingsExpander expander && ReferenceEquals(expander.Footer, previousFooter))
                    expander.Footer = null;
                else if (ctrl is FASettingsExpanderItem item && ReferenceEquals(item.Footer, previousFooter))
                    item.Footer = null;
                ctrl.ClearValue(FooterContentProperty);
            }

            if (args.NewValue is ControlTemplate template &&
                ctrl is FASettingsExpander or FASettingsExpanderItem)
            {
                // Keep the measured action area so hover never changes the header's available width.
                var footer = new ContentControl { Template = template };
                ctrl.SetValue(FooterContentProperty, footer);
                if (ctrl is FASettingsExpander expander)
                    expander.Footer = footer;
                else if (ctrl is FASettingsExpanderItem item)
                    item.Footer = footer;

                ctrl.PointerEntered += OnPointerEntered;
                ctrl.PointerExited += OnPointerExited;
                ctrl.PropertyChanged += OnOwnerPropertyChanged;
                UpdateFooterVisibility(ctrl, ctrl.IsPointerOver || ctrl.IsKeyboardFocusWithin);
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
        if (sender is Control owner)
            UpdateFooterVisibility(owner, true);
    }

    private static void OnPointerExited(object? sender, RoutedEventArgs e)
    {
        if (sender is Control owner)
            UpdateFooterVisibility(owner, owner.IsKeyboardFocusWithin);
    }

    private static void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is Control owner && e.Property == InputElement.IsKeyboardFocusWithinProperty)
            UpdateFooterVisibility(owner, owner.IsPointerOver || owner.IsKeyboardFocusWithin);
    }

    internal static void UpdateFooterVisibility(Control owner, bool isVisible)
    {
        if (owner.GetValue(FooterContentProperty) is not { } footer)
            return;

        footer.Opacity = isVisible ? 1 : 0;
        footer.IsHitTestVisible = isVisible;
    }
}
