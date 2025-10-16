using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Media.Animation;

namespace LMCUI.Pages.AccountPage.AddAccount;

public partial class AddAccountWizard : UserControl
{
    private readonly Action<(bool hasPrev, bool hasNext, bool isFinal)> _buttonStateChanged;
    private readonly Action<(bool hasPrev, bool hasNext)> _stepButtonStateChanged;
    private void StepButtonStateChanged((bool hasPrev, bool hasNext) state)
    {
        if (contentFrm.Content is AddAccountStep step)
        {
            _buttonStateChanged((state.hasPrev, state.hasNext, step.IsFinalStep()));
        }
    }
    public AddAccountWizard(Action<(bool hasPrev, bool hasNext, bool isFinal)> buttonStateChanged)
    {
        _buttonStateChanged = buttonStateChanged;
        _stepButtonStateChanged = StepButtonStateChanged;
        InitializeComponent();
        contentFrm.Content = new IndexStep();
        contentFrm.IsVisible = true;
        if (contentFrm.Content is AddAccountStep step)
        {
            step.Enter(null, _stepButtonStateChanged);
        }
    }
    
    public void NextStep(object? sender, EventArgs e)
    {
        if (contentFrm.Content is AddAccountStep step)
        {
            var next = step.NextStep();
            if(step.IsFinalStep()) return;
            if (next.type != null)
            {
                contentFrm.Navigate(next.type, null, new SlideNavigationTransitionInfo
                    { Effect = SlideNavigationTransitionEffect.FromRight });
                step = contentFrm.Content as AddAccountStep ?? throw new InvalidOperationException();
                _stepButtonStateChanged((step.PreviousStep().type != null, step.NextStep().type != null));
                step.Enter(next.data, _stepButtonStateChanged);
            }
        }
    }
    
    public void PreviousStep(object? sender, EventArgs e)
    {
        if (contentFrm.Content is AddAccountStep step)
        {
            var prev = step.PreviousStep();
            if (prev.type != null)
            {
                contentFrm.Navigate(prev.type, null, new SlideNavigationTransitionInfo
                    { Effect = SlideNavigationTransitionEffect.FromLeft });
                step = contentFrm.Content as AddAccountStep ?? throw new InvalidOperationException();
                _stepButtonStateChanged((step.PreviousStep().type != null, step.NextStep().type != null));
                step.Enter(prev.data, _stepButtonStateChanged);
            }
        }
    }
    
    public void Closed()
    {
        if (contentFrm.Content is AddAccountStep step)
        {
            step.Closed();
        }
    }
}

public abstract class AddAccountStep : UserControl
{
    public abstract void Enter(object? data, Action<(bool hasPrev, bool hasNext)> buttonStateChanged);
    public abstract (Type? type, object? data) NextStep();
    public abstract (Type? type, object? data) PreviousStep();
    public virtual bool IsFinalStep() => false;
    public virtual void Closed() { }
}