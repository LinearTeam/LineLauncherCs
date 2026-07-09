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
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LMC;
using LMC.Basic.Logging;
using LMCCore.Account;
using LMCCore.Account.Model;
using LMCCore.Game.Launching;
using LMCCore.Game.Model;
using LMCCore.Game.Versioning;
using LMCUI.Pages;

namespace LMCUI.Pages.LaunchPage;

public partial class LaunchPage : PageBase
{
    private readonly Logger _logger = new("LaunchPage");
    private readonly VersionManager _versionManager = new();
    private bool _isLaunching;
    private LaunchVersionOption? _selectedVersion;
    private LaunchAccountOption? _selectedAccount;

    public LaunchPage() : base("Pages.LaunchPage.Title", "LaunchPage")
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        VersionCatalogRefreshCoordinator.Refreshed += VersionCatalogRefreshCoordinator_OnRefreshed;
        _ = LoadLaunchOptionsAsync(VersionCatalogRefreshCoordinator.LatestSnapshot);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        VersionCatalogRefreshCoordinator.Refreshed -= VersionCatalogRefreshCoordinator_OnRefreshed;
    }

    async private Task LoadLaunchOptionsAsync(VersionCatalogSnapshot? snapshot = null)
    {
        try
        {
            SetStatus("正在加载本地版本和账户...");

            AccountManager.Load();
            var versions = snapshot?.Versions ?? await _versionManager.ScanSelectedRootVersionsAsync();
            var versionOptions = versions
                .Select(version => new LaunchVersionOption(version))
                .ToList();
            var accountOptions = AccountManager.Accounts
                .Select(account => new LaunchAccountOption(account))
                .ToList();
            var previousVersionName = _selectedVersion?.Version.VersionName;
            var previousAccountName = _selectedAccount?.Account.Name;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                VersionComboBox.ItemsSource = versionOptions;
                AccountComboBox.ItemsSource = accountOptions;

                var versionIndex = versionOptions.FindIndex(option =>
                    string.Equals(option.Version.VersionName, previousVersionName, StringComparison.Ordinal));
                if (versionIndex >= 0)
                {
                    VersionComboBox.SelectedIndex = versionIndex;
                }
                else if (versionOptions.Count > 0)
                {
                    VersionComboBox.SelectedIndex = 0;
                }
                else
                {
                    VersionComboBox.SelectedIndex = -1;
                }

                var accountIndex = accountOptions.FindIndex(option =>
                    string.Equals(option.Account.Name, previousAccountName, StringComparison.Ordinal));
                if (accountIndex >= 0)
                {
                    AccountComboBox.SelectedIndex = accountIndex;
                }
                else if (accountOptions.Count > 0)
                {
                    AccountComboBox.SelectedIndex = 0;
                }
                else
                {
                    AccountComboBox.SelectedIndex = -1;
                }
            });

            if (versionOptions.Count == 0)
            {
                SetStatus("当前没有可启动的本地版本。");
            }
            else if (accountOptions.Count == 0)
            {
                SetStatus("当前没有可用账户。");
            }
            else
            {
                SetStatus("准备就绪，可以开始启动游戏。");
            }

            UpdateLaunchButtonState();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Loading launch page options");
            SetStatus($"加载启动信息失败：{ex.Message}");
            UpdateLaunchButtonState();
        }
    }

    private void VersionCatalogRefreshCoordinator_OnRefreshed(VersionCatalogSnapshot snapshot)
    {
        _ = LoadLaunchOptionsAsync(snapshot);
    }

    private void VersionComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedVersion = VersionComboBox.SelectedItem as LaunchVersionOption;
        UpdateLaunchButtonState();
    }

    private void AccountComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedAccount = AccountComboBox.SelectedItem as LaunchAccountOption;
        UpdateLaunchButtonState();
    }

    async private void LaunchGameButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isLaunching || _selectedVersion?.Version == null || _selectedAccount?.Account == null)
        {
            return;
        }

        var version = _selectedVersion.Version;
        var account = _selectedAccount.Account;

        _isLaunching = true;
        UpdateLaunchButtonState();
        SetCurrentStep(null);
        SetStatus("正在启动游戏...");

        try
        {
            var progress = new Progress<GameLaunchProgress>(report =>
            {
                SetCurrentStep(report.Step);
                SetStatus($"正在执行第 {report.StepIndex}/{report.TotalSteps} 步");
            });

            var result = await Task.Run(
                () => _versionManager.LaunchGameAsync(
                    version,
                    Current.Config,
                    account,
                    shouldValidateAndCompleteMissingFiles: true,
                    progress: progress));

            SetCurrentStep(GameLaunchProgressStep.WaitForGameWindow);
            SetStatus(result.WindowDetected
                ? "启动完成，已检测到游戏窗口。"
                : "启动完成，但暂未检测到游戏窗口。");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Launching game from temporary launch page");
            SetStatus($"启动失败：{ex.Message}");
        }
        finally
        {
            _isLaunching = false;
            UpdateLaunchButtonState();
        }
    }

    private void UpdateLaunchButtonState()
    {
        var isEnabled = !_isLaunching &&
                        _selectedVersion?.Version != null &&
                        _selectedAccount?.Account != null;
        PostToUi(() => LaunchGameButton.IsEnabled = isEnabled);
    }

    private void SetCurrentStep(GameLaunchProgressStep? step)
    {
        var text = step == null
            ? "当前进行的启动步骤：未开始"
            : $"当前进行的启动步骤：{GetStepDisplayText(step.Value)}";
        PostToUi(() => CurrentStepLabel.Text = text);
    }

    private void SetStatus(string status)
    {
        PostToUi(() => CurrentStatusLabel.Text = $"当前状态：{status}");
    }

    private static void PostToUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private static string GetStepDisplayText(GameLaunchProgressStep step)
    {
        return step switch
        {
            GameLaunchProgressStep.PreLaunchCheck => "启动前检查",
            GameLaunchProgressStep.DownloadMissingFiles => "补全资源文件",
            GameLaunchProgressStep.ProcessAccount => "处理账户",
            GameLaunchProgressStep.ProcessLaunchArguments => "处理启动参数",
            GameLaunchProgressStep.ExtractLocalLibraries => "解压本地库文件",
            GameLaunchProgressStep.StartGame => "启动游戏进程",
            GameLaunchProgressStep.WaitForGameWindow => "等待游戏窗口出现",
            _ => step.ToString()
        };
    }
}

internal sealed record LaunchVersionOption(LocalGameVersionEntry Version)
{
    public override string ToString()
    {
        return Version.VersionName;
    }
}

internal sealed record LaunchAccountOption(Account Account)
{
    public override string ToString()
    {
        return $"{Account.Name}-{GetAccountTypeText(Account.Type)}";
    }

    private static string GetAccountTypeText(AccountType type)
    {
        return type switch
        {
            AccountType.Offline => "离线",
            AccountType.Microsoft => "微软",
            AccountType.Authlib => "第三方",
            _ => type.ToString()
        };
    }
}
