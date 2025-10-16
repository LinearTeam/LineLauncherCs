using System;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using LMCCore.Account;
using LMCCore.Account.Model;
using LMCUI.I18n;

namespace LMCUI.Pages.AccountPage.AddAccount.Offline;

public partial class OfflineStep : AddAccountStep
{
    private Action<(bool hasPrev, bool hasNext)>? _buttonStateChanged;
    private bool _isFinal;
    private Account? _account;

    public OfflineStep()
    {
        InitializeComponent();
    }
    public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
    {
        _buttonStateChanged = buttonStateChanged;
        if (data != null)
        {
            var offlineData = ((string? id, string? uuid))data;
            this.ignBox.Text = offlineData.id;
            this.uuidBox.Text = offlineData.uuid;
        }
    }
    public override (Type? type, object? data) NextStep()
    {
        var valid = CheckValidate();
        if (!valid)
        {
            _buttonStateChanged?.Invoke((true, false));
            return (null, null);
        }
        var isLegalId = IsLegal(ignBox.Text);
        if (!isLegalId)
        {
            return (typeof(GameIdWarnStep), (ignBox.Text, uuidBox.Text));
        }
        else
        {
            var name = string.IsNullOrWhiteSpace(ignBox.Text) ? throw new Exception("Game id is null") : ignBox.Text;
            var uuid = string.IsNullOrWhiteSpace(uuidBox.Text) ? AccountManager.GenerateOfflineUuid(name) : uuidBox.Text;
            var account = new OfflineAccount{
                Name = name,
                Uuid = uuid,
                Type = AccountType.Offline
            };
            _account = account;
        }
        return (null, null);
    }
    public override (Type? type, object? data) PreviousStep() => (typeof(IndexStep), 1);
    private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        CheckValidate();
    }

    private bool CheckValidate()
    {
        bool isValid;
        if (string.IsNullOrWhiteSpace(ignBox.Text))
        {
            validate.Text = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.Steps.OfflineStep.ValidationMessages.EmptyGameId");
            validate.IsVisible = true;
            isValid = false;
        }else if (!string.IsNullOrEmpty(uuidBox.Text) && !Guid.TryParse(uuidBox.Text, out _))
        {
            validate.Text = I18nManager.Instance.GetString("Pages.AccountPage.AddAccountWizard.Steps.OfflineStep.ValidationMessages.InvalidUUID");
            validate.IsVisible = true;
            isValid = false;
        }
        else
        {
            validate.IsVisible = false;
            isValid = true;
        }
        if (isValid)
        {
            var isLegalId = IsLegal(ignBox.Text);
            if (isLegalId)
            {
                _isFinal = true;
                _buttonStateChanged?.Invoke((true, true));
            }
            else
            {
                _isFinal = false;
                _buttonStateChanged?.Invoke((true, true));
            }
        }
        else
        {
            _buttonStateChanged?.Invoke((true, false));
        }
        return isValid;
    }

    private bool IsLegal(string? input)
    {
        return Regex.IsMatch(input ?? "-", @"^[a-zA-Z0-9_]+$") && input is{Length: < 16};
    }
    public override bool IsFinalStep()
    {
        return _isFinal;
    }
    public override Account? GetFinalAccount() => _account;
}