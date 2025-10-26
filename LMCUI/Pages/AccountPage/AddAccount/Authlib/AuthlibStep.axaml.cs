using System;

namespace LMCUI.Pages.AccountPage.AddAccount.Authlib;

public partial class AuthlibStep : AddAccountStep
{
    public AuthlibStep()
    {
        InitializeComponent();
    }
    public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
    {
        buttonStateChanged((true, true));
    }
    public override (Type? type, object? data) NextStep() => (null, null);
    public override (Type? type, object? data) PreviousStep() => (typeof(IndexStep), 2);
    public override bool IsFinalStep() => true;
}