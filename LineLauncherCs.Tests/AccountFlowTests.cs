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
using LMCCore.Account;
using LMCCore.Account.Model;
using LMCUI.Pages.AccountPage;
using LMCUI.Pages.AccountPage.AddAccount;

namespace LineLauncherCs.Tests;

public class AccountFlowTests
{
    [Fact]
    public void BuildAccountListPresentation_UsesEmptyDescriptionForNoAccounts()
    {
        var presentation = AccountPageSupport.BuildAccountListPresentation([]);

        Assert.Equal("Pages.AccountPage.AccountListExpander.NoAccountsDescription", presentation.DescriptionKey);
        Assert.Empty(presentation.Accounts);
    }

    [Fact]
    public async Task RefreshMicrosoftAvatarsAsync_TracksAvatarUpdatesAndTokenChanges()
    {
        var accounts = new[]
        {
            new MicrosoftAccount { Name = "One", RefreshToken = "old-1" },
            new MicrosoftAccount { Name = "Two", RefreshToken = "same-2" }
        };

        var summary = await AccountPageSupport.RefreshMicrosoftAvatarsAsync(
            accounts,
            (account, _) =>
            {
                if (account.Name == "One")
                {
                    account.RefreshToken = "new-1";
                    return Task.FromResult(true);
                }

                return Task.FromResult(false);
            });

        Assert.True(summary.HasAvatarUpdate);
        Assert.True(summary.ShouldSaveAccounts);
    }

    [Fact]
    public void AccountAvatarCacheHelper_NormalizesUuidAndBuildsPaths()
    {
        var normalized = AccountAvatarCacheHelper.NormalizeUuid("ABCD-EF12");
        var avatarPath = AccountAvatarCacheHelper.GetAvatarCachePath(@"C:\cache", "ABCD-EF12");
        var skinPath = AccountAvatarCacheHelper.GetSkinUrlCachePath(@"C:\cache", "ABCD-EF12");

        Assert.Equal("abcdef12", normalized);
        Assert.Equal(@"C:\cache\abcdef12.png", avatarPath);
        Assert.Equal(@"C:\cache\abcdef12.url", skinPath);
        Assert.True(AccountAvatarCacheHelper.ShouldUseCachedAvatar("https://skins.example/1", "https://skins.example/1", true));
    }

    [Fact]
    public void AccountDuplicateDetector_MatchesOfflineMicrosoftAndAuthlibRules()
    {
        var existingAccounts = new Account[]
        {
            new OfflineAccount { Name = "Player" },
            new MicrosoftAccount { Uuid = "1234-5678", Name = "Steve" },
            new AuthlibAccount { Username = "demo@example.com", Name = "Demo" }
        };

        Assert.True(AccountDuplicateDetector.IsDuplicate(existingAccounts, new OfflineAccount { Name = "Player" }));
        Assert.True(AccountDuplicateDetector.IsDuplicate(existingAccounts, new MicrosoftAccount { Uuid = "12345678" }));
        Assert.True(AccountDuplicateDetector.IsDuplicate(existingAccounts, new AuthlibAccount { Username = "DEMO@example.com" }));
        Assert.False(AccountDuplicateDetector.IsDuplicate(existingAccounts, new OfflineAccount { Name = "Other" }));
    }

    [Fact]
    public void GenerateOfflineUuid_IsStable()
    {
        var first = AccountManager.GenerateOfflineUuid("Dream");
        var second = AccountManager.GenerateOfflineUuid("Dream");

        Assert.Equal(first, second);
    }

    [Fact]
    public void AddAccountWizardCoordinator_AdvanceAndRetreat_UseStepTransitions()
    {
        var nextType = typeof(AccountFlowTests);
        var previousType = typeof(HelpSystemTests);
        var account = new OfflineAccount { Name = "Dream" };
        var step = new FakeAddAccountStep(
            isFinalStep: false,
            next: new AddAccountWizardTransition(nextType, 7),
            previous: new AddAccountWizardTransition(previousType, 3),
            account: account);

        var advance = AddAccountWizardCoordinator.Advance(step);
        var retreat = AddAccountWizardCoordinator.Retreat(step);

        Assert.False(advance.IsFinalStep);
        Assert.Equal(nextType, advance.Transition.Type);
        Assert.Equal(7, advance.Transition.Data);
        Assert.Equal(previousType, retreat.Type);
        Assert.Equal(3, retreat.Data);
    }

    [Fact]
    public void AddAccountWizardCoordinator_Submit_CapturesFailureWithoutThrowing()
    {
        var account = new OfflineAccount { Name = "Dream" };

        var success = AddAccountWizardCoordinator.Submit(account, _ => { });
        var failure = AddAccountWizardCoordinator.Submit(account, _ => throw new ArgumentException("boom"));

        Assert.True(success.Attempted);
        Assert.True(success.Succeeded);
        Assert.False(failure.Succeeded);
        Assert.IsType<ArgumentException>(failure.Exception);
    }

    private sealed class FakeAddAccountStep(
        bool isFinalStep,
        AddAccountWizardTransition next,
        AddAccountWizardTransition previous,
        Account? account) : AddAccountStep
    {
        private readonly bool _isFinalStep = isFinalStep;
        private readonly AddAccountWizardTransition _next = next;
        private readonly AddAccountWizardTransition _previous = previous;
        private readonly Account? _account = account;

        public override void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged)
        {
            buttonStateChanged((_previous.Type != null, _next.Type != null));
        }

        public override (Type? type, object? data) NextStep() => (_next.Type, _next.Data);

        public override (Type? type, object? data) PreviousStep() => (_previous.Type, _previous.Data);

        public override bool IsFinalStep() => _isFinalStep;

        public override Account? GetFinalAccount() => _account;
    }
}
