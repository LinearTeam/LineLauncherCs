using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LMC.Extensions.UI;

namespace LMCUI.Extensions;

internal static class LMCExtensionUIRegistry
{
    private static readonly Lock s_syncRoot = new();
    private static readonly List<UIExtensionNavigationItem> s_navigationItems = [];

    public static IReadOnlyList<UIExtensionNavigationItem> GetNavigationItems()
    {
        lock (s_syncRoot)
        {
            return s_navigationItems.ToList().AsReadOnly();
        }
    }

    public static void RegisterNavigationItem(UIExtensionNavigationItem navigationItem)
    {
        lock (s_syncRoot)
        {
            s_navigationItems.RemoveAll(item => string.Equals(item.Tag, navigationItem.Tag, StringComparison.OrdinalIgnoreCase));
            s_navigationItems.Add(navigationItem);
        }
    }
}
