using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Accounting.Policies;

/// <summary>
/// Signals that manual journal entries to sensitive account types
/// require dual-control (four-eyes) approval per segregation-of-duties (SoD) policy.
///
/// Business rule: Manual entries to Revenue, Liability, and Equity accounts
/// carry a higher fraud/error risk and must be reviewed by a second approver.
/// This policy SIGNALS the requirement — the application layer is responsible
/// for enforcing the approval workflow.
///
/// Severity: Warning (advisory — does not block the pipeline;
/// the workflow engine acts on this signal).
///
/// Order: 20 (Business Rule tier)
/// </summary>
public sealed class DualControlManualEntryPolicy : PolicyBase<AccountAssignmentContext>
{
    /// <summary>Account types that require dual-control on manual entries.</summary>
    private static readonly IReadOnlySet<AccountType> SensitiveAccountTypes =
        new HashSet<AccountType>
        {
            AccountType.Revenue,
            AccountType.Liability,
            AccountType.Equity
        };

    public override string PolicyName => "Accounting.DualControlManualEntry";
    public override int Order => PolicyOrderingConventions.BusinessRuleMin + 10; // = 20

    public override Task<PolicyResult> EvaluateAsync(
        AccountAssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        // Only applies to manually entered journal entries
        if (!context.IsManualEntry)
            return Task.FromResult(Pass());

        if (SensitiveAccountTypes.Contains(context.AccountType))
        {
            return Task.FromResult(Warn(
                code: AccountingErrorCodes.DualControlRequired,
                message: $"Manual journal entry to {context.AccountType} account " +
                         $"'{context.AccountCode}' requires dual-control (four-eyes) approval " +
                         $"per segregation-of-duties policy. " +
                         $"A second approver must review and authorise this entry " +
                         $"before it is committed to the ledger."));
        }

        return Task.FromResult(Pass());
    }
}
