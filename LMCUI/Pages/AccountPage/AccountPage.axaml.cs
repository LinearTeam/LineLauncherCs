using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using LMCCore.Account;
using LMCCore.Account.Model;
using LMCUI.Pages.AccountPage.AddAccount;

namespace LMCUI.Pages.AccountPage;

public partial class AccountPage : PageBase
{
    public AccountPage() : base("账号管理", "AccountPage")
    {
        InitializeComponent();
    }

    public async Task RefreshAccountList()
    {
        AccountManager.Load();
        var accounts = AccountManager.Accounts;
        await Dispatcher.UIThread.InvokeAsync(() => acclist.ItemsSource = new List<Account>(accounts));
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
    private void Button_AddAccount(object? sender, RoutedEventArgs _)
    {
        var dlg = new ContentDialog();
        dlg.Title = new TextBlock(){
            Text = "添加账户向导",
            FontSize = 15,
            FontWeight = Avalonia.Media.FontWeight.Light
        };
        
        dlg.Content = new AddAccountWizard((state =>
        {
            dlg.IsPrimaryButtonEnabled = state.hasPrev;
            dlg.IsSecondaryButtonEnabled = state.hasNext;
            dlg.SecondaryButtonText = state.isFinal ? "完成" : "下一步";
        }));
        dlg.CloseButtonText = "关闭";
        dlg.PrimaryButtonText = "上一步";
        dlg.SecondaryButtonText = "下一步";
        dlg.DefaultButton = ContentDialogButton.Secondary;
        dlg.IsPrimaryButtonEnabled = false;
        dlg.IsSecondaryButtonEnabled = true;
        dlg.PrimaryButtonClick += (s, e) =>
        {
            e.Cancel = true;
            ((AddAccountWizard)dlg.Content).PreviousStep(s, e);
        };
        dlg.SecondaryButtonClick += (s, e) =>
        {
            e.Cancel = true;
            if(dlg.SecondaryButtonText == "完成") e.Cancel = false;
            ((AddAccountWizard)dlg.Content).NextStep(s, e);
        };
        dlg.CloseButtonClick += (s, e) => { ((AddAccountWizard)dlg.Content).Closed(); };
        dlg.ShowAsync();
    }
}

public class AuthlibDescriptionConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) => $"{values[0]} | {values[1]}";
}