using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMC.Basic.Logging;
using LMCCore.Account;
using LMCCore.Account.Model;
using LMCUI.I18n;
using LMCUI.Pages.AccountPage.AddAccount;
using LMCUI.Utils;

namespace LMCUI.Pages.AccountPage;

public partial class AccountPage : PageBase
{
    private static Logger s_logger = new Logger("AccountPage");
    public AccountPage() : base("Pages.AccountPage.Title", "AccountPage")
    {
        InitializeComponent();
        _ = Task.Run(RefreshAccountList);
    }
    public async Task RefreshAccountList()
    {
        try
        {
            await RefreshAccountListInternal();
        }catch (Exception ex)
        {
            s_logger.Error(ex, "Refreshing account list");
            await MessageQueueHelper.ShowError(I18nManager.Instance.GetString("Messages.AccountPage.Errors.FailedToRefreshAccountList.Title"), 
                I18nManager.Instance.GetString("Messages.AccountPage.Errors.FailedToRefreshAccountList.Content", ex.Message));
        }
    }

    private async Task RefreshAccountListInternal()
    {
        // Why
        await Task.Delay(200);
        AccountManager.Load();
        var accounts = AccountManager.Accounts;
        
        foreach (var account in accounts)
        {
            if (string.IsNullOrEmpty(account.AvatarBase64))
            {
                //Assets/steve.png
                // ReSharper disable once StringLiteralTypo
                account.AvatarBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAABjklEQVR4AezWwUvCUBwH8K+D0tRVRGRJqVBE1MF754LA/oA6WZgQGBRdjeoW1M2gwEtQl24dMgqC6NKhQ4EXo0seFExBihpIzbbWyW87tNOah218YI9tj/e+v21vQjgkqmwsIKosHOxQ2Ui/R2VDvS6VDfvdKuO+fo5HB0SVCbB4swcg1BWA6StSVxUwj6sVLNDlBuvr9IA58HtXtP6YXYLmS2AnEQVLrcTBdleXwZLzc2DrC1GwzZlpMG+bE6z5EtC/BWa3rU9gIzoLpsgOsOqLBFaqVMHypQqYIiva/Q2v729gixPjYNYnYHaNjfq3PgG30wkWSabAjq8HwVzyJ1jQ1wO2n/GBxfZOwdq7/WCCUURmn7cHIDznH8EyazGw+GQZ7OzmFuzk/ApsKVIAO0pMgeVy92B2CaxPQJK/wArFJ7DtgzRYyCviL1vpQ7CqVANzoEX7S2ywPgGzPzRG/VufQL5U1tbzhodiBUw/g4vsHdhlLgumv16qac8Y+dDWEmZ9AvoR/3fb9ASMJvQNAAD//zii3k4AAAAGSURBVAMAKieGVEUw3nEAAAAASUVORK5CYII=";
            }
        }
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            acclist.Description = I18nManager.Instance.GetString(accounts.Count == 0 
                ? "Pages.AccountPage.AccountListExpander.NoAccountsDescription"
                : "Pages.AccountPage.AccountListExpander.Description");
            return acclist.ItemsSource = new List<Account>(accounts);
        });
    }
    
    private void Button_CopyUuid(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.DataContext is Account account)
        {
            MainWindow.Instance.Clipboard?.SetTextAsync(account.Uuid);
        }
    }

    private void Button_CopyUsername(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.DataContext is AuthlibAccount account)
        {
            MainWindow.Instance.Clipboard?.SetTextAsync(account.Username);
        }
    }
    private void Button_AddAccount(object? sender, RoutedEventArgs rea)
    {
        var dlg = new ContentDialog
        {
            Title = new TextBlock{
                Text = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.Title"),
                FontSize = 15,
                FontWeight = Avalonia.Media.FontWeight.Light
            },
            CloseButtonText = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.CloseButton"),
            PrimaryButtonText = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.PreviousButton"),
            SecondaryButtonText = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.NextButton"),
            DefaultButton = ContentDialogButton.Secondary,
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = true
        };

        dlg.Content = new AddAccountWizard((state =>
        {
            dlg.IsPrimaryButtonEnabled = state.hasPrev;
            dlg.IsSecondaryButtonEnabled = state.hasNext;
            dlg.SecondaryButtonText = state.isFinal ? I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.FinishButton") : I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.NextButton");
        }));
        dlg.PrimaryButtonClick += (s, e) =>
        {
            e.Cancel = true;
            ((AddAccountWizard)dlg.Content).PreviousStep(s, e);
        };
        dlg.SecondaryButtonClick += (s, e) =>
        {
            e.Cancel = true;
            if (dlg.SecondaryButtonText == I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.FinishButton"))
            {
                e.Cancel = false;
                _ = Task.Run(RefreshAccountList);
            }
            ((AddAccountWizard)dlg.Content).NextStep(s, e);
        };
        dlg.CloseButtonClick += (s, e) => { ((AddAccountWizard)dlg.Content).Closed(); };
        dlg.ShowAsync();
    }
    private void Button_Delete(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.DataContext is Account account)
        {
            if (button.Parent.Parent is SettingsExpanderItem se)
            {
                se.IsVisible = false;
            }
            AccountManager.Remove(account);
            AccountManager.Load();
            _ = Task.Run(RefreshAccountList);
        }
    }
}

public class AuthlibDescriptionConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) => $"{values[0]} | {values[1]}";
}