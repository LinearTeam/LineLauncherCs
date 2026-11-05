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

using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using LMCUI.Navigation.Model;

namespace LMCUI;

public partial class MainWindow
{
    private bool _isEscapePressed;
    private KeyEventArgs? _escapeNavigationKeyDown;

    private void InitializeKeyboardNavigation()
    {
        AddHandler(KeyDownEvent, PreviewNavigationKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, NavigationKeyDown, RoutingStrategies.Bubble);
        AddHandler(KeyUpEvent, NavigationKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        Deactivated += (_, _) => ResetEscapeNavigation();
    }

    private void PreviewNavigationKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        _escapeNavigationKeyDown = null;
        if (_isEscapePressed)
            return;
        _isEscapePressed = true;

        // Snapshot before controls dismiss popups. ContentDialog handles Escape on KeyUp.
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None && IsEnabled && _isWindowLoaded
            && _isSplashScreenCompleted && _isInitialNavigationCompleted && !_isHandlingDrop
            && !HasEscapeNavigationBlocker(this, e.Source))
            _escapeNavigationKeyDown = e;
    }

    private void NavigationKeyDown(object? sender, KeyEventArgs e)
    {
        if (!ReferenceEquals(_escapeNavigationKeyDown, e) || e.Handled)
            return;
        _escapeNavigationKeyDown = null;

        if (HasEscapeNavigationBlocker(this, e.Source))
            return;

        if (BreadCrumbItemSource.Count > 1)
        {
            var parent = BreadCrumbItemSource[^2];
            e.Handled = true;
            NavigatePage(parent.PageNavigateWay, NavigateType.Backward, parent.Tag);
        }
        else if (mainFrm.CurrentPage is not Pages.LaunchPage.LaunchPage
                 && FindNavigationItemByTag("LaunchPage") is { } launchItem
                 && _navItemTagToPageType.TryGetValue("LaunchPage", out var launchPage))
        {
            e.Handled = true;
            NavigatePage(launchPage, NavigateType.New, launchItem);
        }
    }

    private void NavigationKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            ResetEscapeNavigation();
    }

    private void ResetEscapeNavigation()
    {
        _isEscapePressed = false;
        _escapeNavigationKeyDown = null;
    }

    internal static bool HasEscapeNavigationBlocker(Control root, object? source = null)
    {
        if (source is Visual visual && visual.GetSelfAndVisualAncestors()
                .Any(ancestor => ancestor is FAContentDialog or PopupRoot or OverlayPopupHost or FATeachingTip))
            return true;

        // Logical children include declared Popups; visual children include templates and dialog overlays.
        return root.GetVisualDescendants().OfType<Control>()
            .Concat(root.GetLogicalDescendants().OfType<Control>())
            .Prepend(root)
            .Any(control => control is Popup { IsOpen: true } || control.IsVisible && (control switch
            {
                FAContentDialog => true,
                FADialogHost { Content: FAContentDialog } => true,
                PopupRoot or OverlayPopupHost => true,
                ComboBox { IsDropDownOpen: true } => true,
                AutoCompleteBox { IsDropDownOpen: true } => true,
                MenuItem { IsSubMenuOpen: true } => true,
                FATeachingTip { IsOpen: true } => true,
                Button { Flyout.IsOpen: true } => true,
                _ => control.ContextMenu?.IsOpen == true || control.ContextFlyout?.IsOpen == true
            }));
    }
}
