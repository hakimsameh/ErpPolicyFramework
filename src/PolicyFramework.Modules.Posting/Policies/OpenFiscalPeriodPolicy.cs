using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Posting.Policies;

/// <summary>
/// Prevents posting to a closed or locked fiscal period.
/// Issues an advisory warning when the period is in "Closing" status.
///
/// Business rules:
///   - Closed/Locked periods: hard block (Error)
///   - Closing period: advisory warning (pipeline continues)
///   - Open period: clean pass
///
/// Order: 2 (Hard Gate tier — runs immediately after balance check)
/// </summary>
public sealed class OpenFiscalPeriodPolicy : PolicyBase<PostingContext>
{
    /// <summary>Policy name for bypass/config — use instead of hardcoded string.</summary>
    public const string Name = "Posting.OpenFiscalPeriod";
    public override string PolicyName => Name;
    public override int Order => PolicyOrderingConventions.HardGateMin + 1; // = 2

    public override async Task<PolicyResult> EvaluateAsync(
        PostingContext context,
        CancellationToken cancellationToken = default)
    {
        var periodStatus = await context.PeriodStatus();
        return periodStatus switch
        {
            FiscalPeriodStatus.Locked =>
                Fail(
                    code: PostingErrorCodes.FiscalPeriodClosedOrLocked,
                    message: $"Fiscal period {context.FiscalPeriod}/{context.FiscalYear} " +
                             $"is LOCKED. No postings are permitted. " +
                             $"Contact the finance administration team to unlock.",
                    field: nameof(context.PostingDate)),

            FiscalPeriodStatus.Closed =>
                Fail(
                    code: PostingErrorCodes.FiscalPeriodClosedOrLocked,
                    message: $"Fiscal period {context.FiscalPeriod}/{context.FiscalYear} " +
                             $"is CLOSED. Post to the current open period or " +
                             $"request a period re-opening from finance administration.",
                    field: nameof(context.PostingDate)),

            FiscalPeriodStatus.Closing =>
                Warn(
                    code: PostingErrorCodes.FiscalPeriodClosing,
                    message: $"Fiscal period {context.FiscalPeriod}/{context.FiscalYear} " +
                             $"is in CLOSING status. Month-end procedures may be in progress. " +
                             $"Confirm with the financial controller before proceeding.",
                    field: nameof(context.PostingDate)),

            _ => Pass()
        };
    }
}
