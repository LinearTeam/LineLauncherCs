namespace LMC.Extensions.UI;

public interface IUIExtensionApi
{
    void RegisterPage(UIExtensionPageRegistration registration);

    void RegisterNavigationItem(UIExtensionNavigationItem navigationItem);
}
