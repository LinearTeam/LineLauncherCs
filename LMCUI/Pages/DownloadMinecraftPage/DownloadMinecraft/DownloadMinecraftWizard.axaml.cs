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
using Avalonia.Controls;
using FluentAvalonia.UI.Media.Animation;

namespace LMCUI.Pages.DownloadMinecraftPage.DownloadMinecraft;

public partial class DownloadMinecraftWizard : UserControl
{
    private readonly Action<(bool hasPrev, bool hasNext, bool isFinal)> _buttonStateChanged;
    private readonly Action<(bool hasPrev, bool hasNext)> _stepButtonStateChanged;

    public DownloadableVersionSelection? Result => (contentFrm.Content as DownloadMinecraftStep)?.GetResult();

    public DownloadMinecraftWizard(
        DownloadMinecraftWizardContext context,
        Action<(bool hasPrev, bool hasNext, bool isFinal)> buttonStateChanged)
    {
        _buttonStateChanged = buttonStateChanged;
        _stepButtonStateChanged = StepButtonStateChanged;
        InitializeComponent();
        contentFrm.Content = new LoaderSelectionStep();
        contentFrm.IsVisible = true;
        if (contentFrm.Content is DownloadMinecraftStep step)
        {
            step.Enter(context, _stepButtonStateChanged);
        }
    }

    private void StepButtonStateChanged((bool hasPrev, bool hasNext) state)
    {
        if (contentFrm.Content is DownloadMinecraftStep step)
        {
            _buttonStateChanged(DownloadMinecraftWizardSupport.BuildDialogButtonState(step, state));
        }
    }

    public bool Continue()
    {
        if (contentFrm.Content is not DownloadMinecraftStep step)
            return false;

        var next = step.NextStep();
        if (step.IsFinalStep())
        {
            return step.GetResult() != null;
        }

        if (next.type != null)
        {
            contentFrm.Navigate(next.type, null, new FASlideNavigationTransitionInfo
            {
                Effect = FASlideNavigationTransitionEffect.FromRight
            });

            if (contentFrm.Content is DownloadMinecraftStep nextStep)
            {
                _stepButtonStateChanged((nextStep.PreviousStep().type != null, nextStep.NextStep().type != null));
                nextStep.Enter(next.data, _stepButtonStateChanged);
            }
        }

        return false;
    }

    public void NextStep(object? sender, EventArgs e)
    {
        _ = Continue();
    }

    public void PreviousStep(object? sender, EventArgs e)
    {
        if (contentFrm.Content is not DownloadMinecraftStep step)
            return;

        var prev = step.PreviousStep();
        if (prev.type != null)
        {
            step.BackToPrevious();
            contentFrm.Navigate(prev.type, null, new FASlideNavigationTransitionInfo
            {
                Effect = FASlideNavigationTransitionEffect.FromLeft
            });

            if (contentFrm.Content is DownloadMinecraftStep prevStep)
            {
                _stepButtonStateChanged((prevStep.PreviousStep().type != null, prevStep.NextStep().type != null));
                prevStep.Enter(prev.data, _stepButtonStateChanged);
            }
        }
    }
}

public abstract class DownloadMinecraftStep : UserControl
{
    public abstract void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged);
    public abstract (Type? type, object? data) NextStep();
    public abstract (Type? type, object? data) PreviousStep();
    public virtual bool IsFinalStep() => false;
    public virtual void BackToPrevious() { }
    public virtual DownloadableVersionSelection? GetResult() => null;
}

public sealed record DownloadMinecraftWizardContext(string SelectedRootPath, string ManifestVersionId);

    public sealed record DownloadMinecraftSelectionContext(
        string SelectedRootPath,
        string ManifestVersionId,
        string? FabricVersion,
        string? ForgeVersion,
        string? OptiFineVersion);

public sealed record DownloadableVersionSelection(
    string ManifestVersionId,
    string VersionName,
    string SelectedRootPath,
    string? FabricVersion,
    string? ForgeVersion,
    string? OptiFineVersion);
