using System;
using LMC.Extensions.UI;
using LMCUI.Navigation;
using LMCUI.Navigation.Model;
using LMCUI.Pages;

namespace LMCUI.Extensions;

internal sealed class LMCExtensionUIApi : IUIExtensionApi
{
    public void RegisterPage(UIExtensionPageRegistration registration)
    {
        if (!typeof(PageBase).IsAssignableFrom(registration.PageType))
        {
            throw new ArgumentException($"Page type {registration.PageType.FullName} must inherit from PageBase.");
        }

        PageRegistry.Instance.Register(new PageRegistration
        {
            PageType = registration.PageType,
            StaticTag = registration.StaticTag,
            StorageMode = MapStorageMode(registration.StorageMode),
            SupportsDynamicTag = registration.SupportsDynamicTag,
            DynamicTagPrefix = registration.DynamicTagPrefix,
            GetCacheKey = registration.GetCacheKey
        });
    }

    public void RegisterNavigationItem(UIExtensionNavigationItem navigationItem)
    {
        LMCExtensionUIRegistry.RegisterNavigationItem(navigationItem);
    }

    private static PageStorageMode MapStorageMode(UIExtensionPageStorageMode storageMode)
    {
        return storageMode switch
        {
            UIExtensionPageStorageMode.Transient => PageStorageMode.Transient,
            UIExtensionPageStorageMode.Singleton => PageStorageMode.Singleton,
            UIExtensionPageStorageMode.Parameterized => PageStorageMode.Parameterized,
            _ => PageStorageMode.Singleton
        };
    }
}
