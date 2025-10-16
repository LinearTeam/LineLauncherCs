using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LMCUI.Pages.AccountPage.AddAccount;

public partial class IndexStep : AddAccountStep
{
    public IndexStep()
    {
        InitializeComponent();
    }
    public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
    {
        accTypeList.SelectedIndex = (data as int?) ?? 0;
    }
    public override (Type? type, object? data) NextStep()
    {
        switch (accTypeList.SelectedIndex)
        {
            case 0:
                return (typeof(Microsoft.MicrosoftStep), null);
            case 1:
                return (typeof(Offline.OfflineStep), null);
            case 2:
                return (typeof(Authlib.AuthlibStep), null);
            default:
                return (typeof(Microsoft.MicrosoftStep), null);
        }
    }
    public override (Type? type, object? data) PreviousStep()
    {
        return (null, null);
    }
}