namespace LMCUI.Behaviors;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using System;
using Avalonia.Markup.Xaml.Templates;

public static class HoverFooterBehavior
{
    public static readonly AttachedProperty<ControlTemplate> FooterContentTemplateProperty =
        AvaloniaProperty.RegisterAttached<Control, ControlTemplate>(
            "FooterContentTemplate", 
            typeof(HoverFooterBehavior));

    public static ControlTemplate? GetFooterContentTemplate(Control obj) =>
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

    private static void OnPointerEntered(object sender, RoutedEventArgs e)
    {
        if (sender is SettingsExpander expander)
            expander.Footer = CreateFooterContent(expander);
        else if (sender is SettingsExpanderItem item)
            item.Footer = CreateFooterContent(item);
    }

    // 修复模板加载问题
    private static Control? CreateFooterContent(Control owner)
    {
        var template = GetFooterContentTemplate(owner);
        if (template == null) return null;

        // 正确加载模板
        var contentControl = new ContentControl
        {
            Template = template
        };
        return contentControl;
    }

    private static void OnPointerExited(object sender, RoutedEventArgs e)
    {
        if (sender is SettingsExpander expander)
            expander.Footer = null;
        else if (sender is SettingsExpanderItem item)
            item.Footer = null;
    }
}