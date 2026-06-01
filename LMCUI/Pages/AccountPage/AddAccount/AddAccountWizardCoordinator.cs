using System;
using LMCCore.Account.Model;

namespace LMCUI.Pages.AccountPage.AddAccount;

internal readonly record struct AddAccountWizardTransition(Type? Type, object? Data);

internal readonly record struct AddAccountWizardAdvanceResult(
    bool IsFinalStep,
    Account? Account,
    AddAccountWizardTransition Transition);

internal readonly record struct AddAccountSubmissionResult(
    Account? Account,
    bool Succeeded,
    Exception? Exception)
{
    public bool Attempted => Account is not null;
}

internal static class AddAccountWizardCoordinator
{
    public static (bool hasPrev, bool hasNext, bool isFinal) BuildDialogState(
        AddAccountStep step,
        (bool hasPrev, bool hasNext) state)
    {
        return (state.hasPrev, state.hasNext, step.IsFinalStep());
    }

    public static AddAccountWizardAdvanceResult Advance(AddAccountStep step)
    {
        var next = step.NextStep();
        return step.IsFinalStep()
            ? new AddAccountWizardAdvanceResult(true, step.GetFinalAccount(), default)
            : new AddAccountWizardAdvanceResult(false, null, new AddAccountWizardTransition(next.type, next.data));
    }

    public static AddAccountWizardTransition Retreat(AddAccountStep step)
    {
        var previous = step.PreviousStep();
        return new AddAccountWizardTransition(previous.type, previous.data);
    }

    public static AddAccountSubmissionResult Submit(Account? account, Action<Account> addAccount)
    {
        if (account == null)
        {
            return new AddAccountSubmissionResult(null, false, null);
        }

        try
        {
            addAccount(account);
            return new AddAccountSubmissionResult(account, true, null);
        }
        catch (Exception ex)
        {
            return new AddAccountSubmissionResult(account, false, ex);
        }
    }
}
