using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LMCUI.Pages.AccountPage.AddAccount.Microsoft;

public partial class MicrosoftStep : AddAccountStep
{
    public MicrosoftStep()
    {
        InitializeComponent();
    }
    public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged) { }
    public override (Type? type, object? data) NextStep() => (null, null);
    public override (Type? type, object? data) PreviousStep() => (typeof(IndexStep), 0);
}