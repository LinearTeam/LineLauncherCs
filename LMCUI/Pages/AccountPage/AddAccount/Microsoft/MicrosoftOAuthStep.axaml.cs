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
using System.Threading.Tasks;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LMC.Basic.Logging;
using LMCCore.Account.Model;
using LMCCore.Account.OAuth;
using LMCUI.I18n;
using LMCUI.Utils;

namespace LMCUI.Pages.AccountPage.AddAccount.Microsoft;

public partial class MicrosoftOAuthStep : AddAccountStep
{
    private static Logger s_logger = new Logger("OAStep");
    
    private bool _success = false;
    private Account? _account = null;
    public MicrosoftOAuthStep()
    {
        InitializeComponent();
    }

    
    public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
    {
        buttonStateChanged((true, false));
        LoginStatusPanel.IsVisible = false;
        LinkPanel.IsVisible = true;
        CompletePanel.IsVisible = false;
        _account = null;
        _success = false;
        _ = Task.Run(async () =>
        {
            var account = await MicrosoftOAuth.StartOAuth((report) =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (report.Step == -10)
                    {
                        s_logger.Warn("已取消。");
                        buttonStateChanged((true, true));
                        return;
                    }
                    buttonStateChanged((true, false));
                    if (report.Step == 1)
                    {
                        LoginStatusPanel.IsVisible = false;
                        LinkPanel.IsVisible = true;
                        CompletePanel.IsVisible = false;
                        return;
                    }
                    LoginStatusPanel.IsVisible = true;
                    LinkPanel.IsVisible = false;
                    CompletePanel.IsVisible = false;
                    StatusProgRing.IsActive = true;
                    StatusProgRing.IsIndeterminate = true;
                    var translatedMsg = I18nManager.Instance.GetString(report.Message);
                    if (report.Step <= 0 || report.Step > report.TotalStep)
                    {
                        StepMessage.Text = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.Steps.MicrosoftOAuthStep.ErrorMessage", translatedMsg);
                        s_logger.Error($"步骤 {report.Step} / {report.TotalStep}: {report.Message}");
                        StatusProgRing.IsActive = false;
                        StatusProgRing.IsIndeterminate = false;
                        
                        buttonStateChanged((true, false));
                        if (report.Step == -2)
                        {
                            StepMessage.Text = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.Steps.MicrosoftOAuthStep.NoMinecraftProfileMessage");
                        }
                        return;
                    }
                    StepMessage.Text = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.Steps.MicrosoftOAuthStep.StepMessage", report.Step, report.TotalStep, translatedMsg);
                    s_logger.Info($"步骤 {report.Step} / {report.TotalStep}: {report.Message}");
                });
            });
            if(account == null) return;
            _account = account;
            _success = true;
            Dispatcher.UIThread.Invoke(() =>
            {
                
                LoginStatusPanel.IsVisible = false;
                LinkPanel.IsVisible = false;
                CompletePanel.IsVisible = true;
                buttonStateChanged((true, true));
            });
        });
    }
    public override Account? GetFinalAccount() => _account;
    public override (Type? type, object? data) NextStep() => (null, null);
    public override (Type? type, object? data) PreviousStep() => (typeof(MicrosoftStep), null);
    public override void BackToPrevious()
    {
        try
        {
            MicrosoftOAuth.CancelOAuth();
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "CancelOAuth");
        }
    }
    public override void Closed()
    {
        try
        {
            MicrosoftOAuth.CancelOAuth();
        }
        catch (Exception ex)
        {
            s_logger.Error(ex, "CancelOAuth");
        }
    }
    public override bool IsFinalStep() => true;
    private void Button_CopyLink(object? sender, RoutedEventArgs e)
    {
        var link = MicrosoftOAuth.LoginUrl;
        MainWindow.Instance.Clipboard?.SetTextAsync(link);
    }
    private void Button_OpenLink(object? sender, RoutedEventArgs e)
    {
        var link = MicrosoftOAuth.LoginUrl;
        CrossPlatformUtils.OpenUrl(link);
    }
}