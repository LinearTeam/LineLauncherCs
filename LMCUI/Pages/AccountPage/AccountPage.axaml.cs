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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input.Platform;
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

    async private Task RefreshAccountListInternal()
    {
        s_logger.Info("开始刷新账号列表");
        await Task.Delay(200);
        AccountManager.Load();
        var accounts = AccountManager.Accounts.ToList();
        s_logger.Info($"已加载 {accounts.Count} 个账号");
        
        foreach (var account in accounts)
        {
            AccountAvatarService.ApplyCachedOrDefaultAvatar(account);
        }
        s_logger.Info("已为账号列表应用默认头像或本地缓存头像");
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            acclist.Description = I18nManager.Instance.GetString(accounts.Count == 0 
                ? "Pages.AccountPage.AccountListExpander.NoAccountsDescription"
                : "Pages.AccountPage.AccountListExpander.Description");
            return acclist.ItemsSource = new List<Account>(accounts);
        });
        s_logger.Info("账号列表已提交到 UI");

        _ = Task.Run(async () =>
        {
            s_logger.Info("开始后台刷新微软账号头像");
            bool hasAvatarUpdate = false;
            bool shouldSaveAccounts = false;

            foreach (var account in accounts.OfType<MicrosoftAccount>())
            {
                s_logger.Info($"开始刷新微软账号头像: {account.Name} ({account.Uuid})");
                var previousRefreshToken = account.RefreshToken;
                var avatarUpdated = await AccountAvatarService.TryUpdateMicrosoftAvatarAsync(account);
                hasAvatarUpdate |= avatarUpdated;
                shouldSaveAccounts |= !string.Equals(previousRefreshToken, account.RefreshToken, StringComparison.Ordinal);
                s_logger.Info(avatarUpdated
                    ? $"微软账号头像已更新: {account.Name}"
                    : $"微软账号头像未更新，继续使用现有头像: {account.Name}");
            }

            if (shouldSaveAccounts)
            {
                s_logger.Info("检测到微软账号令牌更新，正在保存账号数据");
                AccountManager.Save();
            }

            if (!hasAvatarUpdate)
            {
                s_logger.Info("后台头像刷新完成，没有需要回写到 UI 的头像变更");
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                acclist.ItemsSource = new List<Account>(accounts);
            });
            s_logger.Info("后台头像刷新完成，已将最新头像回写到 UI");
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
        var dlg = new FAContentDialog
        {
            Title = new TextBlock{
                Text = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.Title"),
                FontSize = 15,
                FontWeight = Avalonia.Media.FontWeight.Light
            },
            CloseButtonText = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.CloseButton"),
            PrimaryButtonText = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.PreviousButton"),
            SecondaryButtonText = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.NextButton"),
            DefaultButton = FAContentDialogButton.Secondary,
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
        dlg.CloseButtonClick += (_, _) => { ((AddAccountWizard)dlg.Content).Closed(); };
        dlg.ShowAsync();
    }
    private void Button_Delete(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button?.DataContext is not Account account)
            return;
        if (button.Parent?.Parent is FASettingsExpanderItem se)
        {
            se.IsVisible = false;
        }
        AccountManager.Remove(account);
        AccountManager.Load();
        _ = Task.Delay(250)
            .ContinueWith(async _ =>
            {
                await RefreshAccountList();
            });
    }
}

public class AuthlibDescriptionConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) => $"{values[0]} | {values[1]}";
}
