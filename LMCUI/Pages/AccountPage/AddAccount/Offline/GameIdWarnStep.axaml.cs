using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using LMCCore.Account;
using LMCCore.Account.Model;

namespace LMCUI.Pages.AccountPage.AddAccount.Offline;

public partial class GameIdWarnStep : AddAccountStep
{
    private string? _gameId;
    private string? _uuid;
    public GameIdWarnStep()
    {
        InitializeComponent();
    }
    public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
    {
        var offlineData = ((string? id, string? uuid))data!;
        _gameId = offlineData.id;
        _uuid = offlineData.uuid;
    }
    public override (Type? type, object? data) NextStep()
    {
        var uuid = string.IsNullOrWhiteSpace(_uuid) ? Guid.NewGuid().ToString() : _uuid;
        var name = string.IsNullOrWhiteSpace(_gameId) ? throw new Exception("Game id is null") : _gameId;
        var account = new OfflineAccount{
            Name = name,
            Uuid = uuid,
            Type = AccountType.Offline,
        };
        //TODO: Compute UUID and skin
        AccountManager.Add(account);
        return (null, null);
    }
    public override (Type? type, object? data) PreviousStep() => (typeof(OfflineStep), null);
    public override bool IsFinalStep() => true;
}