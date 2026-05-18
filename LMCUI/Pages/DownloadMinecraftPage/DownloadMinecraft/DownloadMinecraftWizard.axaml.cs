using System;
using Avalonia.Controls;
using FluentAvalonia.UI.Media.Animation;

namespace LMCUI.Pages.DownloadMinecraftPage;

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
            _buttonStateChanged((state.hasPrev, state.hasNext, step.IsFinalStep()));
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
