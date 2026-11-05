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

namespace LMCUI.Controls.Metadata;

public static class MetadataLayoutBehavior
{
    public static readonly AttachedProperty<double> NarrowWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "NarrowWidth", typeof(MetadataLayoutBehavior));

    static MetadataLayoutBehavior()
    {
        NarrowWidthProperty.Changed.AddClassHandler<Control>((control, _) =>
        {
            control.SizeChanged -= OnSizeChanged;
            if (GetNarrowWidth(control) > 0)
                control.SizeChanged += OnSizeChanged;
            UpdateClass(control);
        });
    }

    public static double GetNarrowWidth(Control control) => control.GetValue(NarrowWidthProperty);

    public static void SetNarrowWidth(Control control, double value) =>
        control.SetValue(NarrowWidthProperty, value);

    private static void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged && sender is Control control)
            UpdateClass(control);
    }

    private static void UpdateClass(Control control) =>
        control.Classes.Set("narrow", GetNarrowWidth(control) > 0
            && control.Bounds.Width < GetNarrowWidth(control));
}
