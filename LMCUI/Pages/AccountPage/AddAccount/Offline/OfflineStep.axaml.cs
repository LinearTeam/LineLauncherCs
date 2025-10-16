using System;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LMCUI.Pages.AccountPage.AddAccount.Offline;

public partial class OfflineStep : AddAccountStep
{
    private Action<(bool hasPrev, bool hasNext)>? _buttonStateChanged;
    public OfflineStep()
    {
        InitializeComponent();
    }
    public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
    {
        _buttonStateChanged = buttonStateChanged;
    }
    public override (Type? type, object? data) NextStep()
    {
        var valid = CheckValidate();
        if (!valid)
        {
            _buttonStateChanged?.Invoke((true, false));
            return (null, null);
        }
        var isLegalId = Regex.IsMatch(ignBox.Text ?? "-", @"^[a-zA-Z0-9_]+$");
        if (!isLegalId)
        {
            return (typeof(GameIdWarnStep), (ignBox.Text, uuidBox.Text));
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
        bool isValid = true;
        if (string.IsNullOrWhiteSpace(ignBox.Text))
        {
            validate.Text = "请输入游戏 ID！";
            validate.IsVisible = true;
            isValid = false;
        }else if (!string.IsNullOrEmpty(uuidBox.Text) && !Guid.TryParse(uuidBox.Text, out _))
        {
            validate.Text = "无效的 UUID！";
            validate.IsVisible = true;
            isValid = false;
        }
        else
        {
            validate.IsVisible = false;
            isValid = true;
        }
        _buttonStateChanged?.Invoke((true, isValid));
        return isValid;
    }
}