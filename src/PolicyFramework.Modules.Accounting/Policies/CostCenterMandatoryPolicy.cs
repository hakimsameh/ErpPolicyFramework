using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Accounting.Policies;

/// <summary>
/// Enforces cost-center assignment on accounts that require it.
///
/// Business rule: Expense and revenue accounts flagged as cost-center-required
/// must carry a valid (non-empty) cost-center code. This is enforced for
/// management reporting and departmental P&amp;L accuracy.
///
/// Order: 10 (Business Rule tier)
/// </summary>
public sealed class CostCenterMandatoryPolicy : PolicyBase<AccountAssignmentContext>
{
    /// <summary>Policy name for bypass/config — use instead of hardcoded string.</summary>
    public const string Name = "Accounting.CostCenterMandatory";
    public override string PolicyName => Name;
    public override int Order => PolicyOrderingConventions.BusinessRuleMin; // = 10

    public override Task<PolicyResult> EvaluateAsync(
        AccountAssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        // Policy only applies when the account is flagged as requiring a cost center
        if (!context.RequiresCostCenter)
            return Task.FromResult(Pass());

        if (string.IsNullOrWhiteSpace(context.CostCenter))
        {
            return Task.FromResult(Fail(
                code: AccountingErrorCodes.CostCenterMissing,
                message: $"Account '{context.AccountCode}' ({context.AccountType}) " +
                         $"is configured to require a cost center assignment. " +
                         $"Specify a valid cost center before posting.",
                field: nameof(context.CostCenter)));
        }

        return Task.FromResult(Pass());
    }
}
