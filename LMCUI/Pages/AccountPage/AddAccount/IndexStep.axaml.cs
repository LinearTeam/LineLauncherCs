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