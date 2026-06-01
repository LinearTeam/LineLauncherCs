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
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Media.Animation;
using LMCCore.Account;
using LMCCore.Account.Model;
using LMC.Basic.Logging;
using LMCUI.Controls;
using LMCUI.I18n;

namespace LMCUI.Pages.AccountPage.AddAccount;

public partial class AddAccountWizard : UserControl
{
    private readonly static Logger s_logger = new("AddAccountWizard");
    private readonly Action<(bool hasPrev, bool hasNext, bool isFinal)> _buttonStateChanged;
    private readonly Action<(bool hasPrev, bool hasNext)> _stepButtonStateChanged;
    private void StepButtonStateChanged((bool hasPrev, bool hasNext) state)
    {
        if (contentFrm.Content is AddAccountStep step)
        {
            _buttonStateChanged(AddAccountWizardCoordinator.BuildDialogState(step, state));
        }
    }
    public AddAccountWizard(Action<(bool hasPrev, bool hasNext, bool isFinal)> buttonStateChanged)
    {
        _buttonStateChanged = buttonStateChanged;
        _stepButtonStateChanged = StepButtonStateChanged;
        InitializeComponent();
        contentFrm.Content = new IndexStep();
        contentFrm.IsVisible = true;
        if (contentFrm.Content is AddAccountStep step)
        {
            step.Enter(null, _stepButtonStateChanged);
        }
    }
    
    public void NextStep(object? sender, EventArgs e)
    {
        if (contentFrm.Content is AddAccountStep step)
        {
            var advance = AddAccountWizardCoordinator.Advance(step);
            if (advance.IsFinalStep)
            {
                var submission = AddAccountWizardCoordinator.Submit(advance.Account, AccountManager.Add);
                if (submission.Attempted && submission.Account != null)
                {
                    var account = submission.Account;
                    if (submission.Succeeded)
                    {
                        s_logger.Info($"添加账户: {account.Name} (类型: {account.Type})");
                        s_logger.Info($"成功添加: {account.Name} (类型: {account.Type})");
                        var typeMsg = I18nManager.Instance.GetString("Enums.AccountType." + account.Type);
                        MessageQueueControl.Instance.AddInfoBar(I18nManager.Instance.GetString("Messages.AccountManager.AddAccount.Success.Title"), I18nManager.Instance.GetString("Messages.AccountManager.AddAccount.Success.Content", typeMsg, account.Name), FAInfoBarSeverity.Success);
                    }
                    else if (submission.Exception != null)
                    {
                        var ex = submission.Exception;
                        s_logger.Error(ex, $"添加账户{account.Name} (类型: {account.Type})");
                        var translatedException = I18nManager.Instance.GetString(ex.Message);
                        MessageQueueControl.Instance.AddInfoBar(I18nManager.Instance.GetString("Messages.AccountManager.AddAccount.Failed.Title"), I18nManager.Instance.GetString("Messages.AccountManager.AddAccount.Failed.Content", translatedException), FAInfoBarSeverity.Error);
                    }
                }
                return;
            }
            if (advance.Transition.Type != null)
            {
                contentFrm.Navigate(advance.Transition.Type, null, new FASlideNavigationTransitionInfo
                    { Effect = FASlideNavigationTransitionEffect.FromRight });
                step = contentFrm.Content as AddAccountStep ?? throw new InvalidOperationException();
                _stepButtonStateChanged((step.PreviousStep().type != null, step.NextStep().type != null));
                step.Enter(advance.Transition.Data, _stepButtonStateChanged);
            }
        }
    }
    
    public void PreviousStep(object? sender, EventArgs e)
    {
        if (contentFrm.Content is AddAccountStep step)
        {
            var previous = AddAccountWizardCoordinator.Retreat(step);
            if (previous.Type != null)
            {
                step.BackToPrevious();
                contentFrm.Navigate(previous.Type, null, new FASlideNavigationTransitionInfo
                    { Effect = FASlideNavigationTransitionEffect.FromLeft });
                step = contentFrm.Content as AddAccountStep ?? throw new InvalidOperationException();
                _stepButtonStateChanged((step.PreviousStep().type != null, step.NextStep().type != null));
                step.Enter(previous.Data, _stepButtonStateChanged);
            }
        }
    }
    
    public void Closed()
    {
        if (contentFrm.Content is AddAccountStep step)
        {
            step.Closed();
        }
    }
}

public abstract class AddAccountStep : UserControl
{
    public abstract void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged);
    public abstract (Type? type, object? data) NextStep();
    public abstract (Type? type, object? data) PreviousStep();
    public virtual bool IsFinalStep() => false;
    public virtual void Closed() { }
    public virtual Account? GetFinalAccount() => null;
    public virtual void BackToPrevious(){ }
}
