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
    private Account? _account;
    
    public GameIdWarnStep()
    {
        InitializeComponent();
    }
    public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
    {
        var offlineData = ((string? id, string? uuid))data!;
        _gameId = offlineData.id;
        _uuid = offlineData.uuid;
        buttonStateChanged.Invoke((true, true));
    }
    public override (Type? type, object? data) NextStep()
    {
        if (_gameId == null) return (null, null);
        var name = string.IsNullOrWhiteSpace(_gameId) ? throw new Exception("Game id is empty") : _gameId;
        var uuid = string.IsNullOrWhiteSpace(_uuid) ? AccountManager.GenerateOfflineUuid(name) : _uuid;
        var account = new OfflineAccount{
            Name = name,
            Uuid = uuid,
            Type = AccountType.Offline
        };
        _account = account;
        return (null, null);
    }
    public override (Type? type, object? data) PreviousStep() => (typeof(OfflineStep), (_gameId, _uuid));
    public override bool IsFinalStep() => true;
    public override Account? GetFinalAccount() => _account;
}