using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LMCCore.Account;
using LMCCore.Account.Model;

namespace LMCUI.Pages.AccountPage;

public partial class AccountPage : PageBase
{
    public AccountPage() : base("账号管理", "AccountPage")
    {
        InitializeComponent();
        acclist.Items.Add(new MicrosoftAccount(){
            Name = "AzogMine",
            Type = AccountType.Microsoft,
            Uuid = "1234567890"
        });
        acclist.Items.Add(new OfflineAccount(){
            Name = "AzogMine",
            Type = AccountType.Offline,
            Uuid = "1234567890"
        });
        acclist.Items.Add(new AuthlibAccount(){
            Name = "AzogMine",
            Type = AccountType.Authlib,
            Uuid = "1234567890",
            Username = "2353426@123.com"
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
}

public class AuthlibDescriptionConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) => $"{values[0]} | {values[1]}";
}