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