using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Accounting.Policies;

/// <summary>
/// Prevents postings to Blocked or Inactive GL accounts.
///
/// Business rules:
///   - Blocked: administrative hold — hard block (Error)
///   - Inactive: account is dormant — hard block (Error)
///   - Active: postings are permitted
///
/// Order: 1 (first Hard Gate — fastest check, eliminates invalid accounts immediately)
/// </summary>
public sealed class ActiveAccountPolicy : PolicyBase<AccountAssignmentContext>
{
    public override string PolicyName => "Accounting.ActiveAccount";
    public override int Order => PolicyOrderingConventions.HardGateMin; // = 1

    public override Task<PolicyResult> EvaluateAsync(
        AccountAssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        return context.AccountStatus switch
        {
            AccountStatus.Blocked =>
                Task.FromResult(Fail(
                    code: AccountingErrorCodes.AccountBlocked,
                    message: $"Account '{context.AccountCode}' ({context.AccountDescription}) " +
                             $"is BLOCKED and cannot receive postings. " +
                             $"Contact the chart-of-accounts administrator to unblock.",
                    field: nameof(context.AccountCode))),

            AccountStatus.Inactive =>
                Task.FromResult(Fail(
                    code: AccountingErrorCodes.AccountInactive,
                    message: $"Account '{context.AccountCode}' ({context.AccountDescription}) " +
                             $"is INACTIVE. Reactivate the account before posting, " +
                             $"or redirect to an active alternative account.",
                    field: nameof(context.AccountCode))),

            AccountStatus.Active =>
                Task.FromResult(Pass()),

            _ =>
                Task.FromResult(Fail(
                    code: AccountingErrorCodes.UnknownStatus,
                    message: $"Account '{context.AccountCode}' has an unknown status " +
                             $"'{context.AccountStatus}'. Posting is blocked pending investigation.",
                    severity: PolicySeverity.Critical,
                    field: nameof(context.AccountStatus)))
        };
    }
}
