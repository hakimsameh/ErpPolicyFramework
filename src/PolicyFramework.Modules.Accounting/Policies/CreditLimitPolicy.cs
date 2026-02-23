using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Accounting.Policies;

/// <summary>
/// Enforces credit limit constraints on accounts that have a configured limit.
///
/// Business rules:
///   1. If posting would push the balance beyond the credit limit → hard block (Error)
///   2. If resulting utilisation is >= 90% → advisory warning (pipeline continues)
///   3. If no credit limit is configured → skip (Pass)
///
/// Order: 15 (Business Rule tier)
/// </summary>
public sealed class CreditLimitPolicy : PolicyBase<AccountAssignmentContext>
{
    /// <summary>Utilisation threshold at which a warning is emitted. Default: 90%.</summary>
    private readonly decimal _warningThreshold;

    public CreditLimitPolicy(decimal warningThreshold = 0.90m)
    {
        _warningThreshold = warningThreshold;
    }

    public override string PolicyName => "Accounting.CreditLimit";
    public override int Order => PolicyOrderingConventions.BusinessRuleMin + 5; // = 15

    public override async Task<PolicyResult> EvaluateAsync(
        AccountAssignmentContext context,
        CancellationToken cancellationToken = default)
    {
        // Skip when credit limit or balance providers are not configured
        if (context.CreditLimit is null || context.CurrentBalance is null)
            return Pass();

        var creditLimit = await context.CreditLimit();
        var currentBalance = await context.CurrentBalance();

        // Skip when no credit limit or balance data is available
        if (creditLimit is null || currentBalance is null)
            return Pass();

        var projectedBalance = currentBalance.Value + context.Amount;
        var limitValue = creditLimit.Value;

        // Rule 1: Hard breach
        if (projectedBalance > limitValue)
        {
            return Fail(
                code: AccountingErrorCodes.CreditLimitBreached,
                message: $"Posting to account '{context.AccountCode}' would breach the " +
                         $"configured credit limit. " +
                         $"Projected balance: {projectedBalance:N2} {context.Currency}, " +
                         $"Credit limit: {limitValue:N2} {context.Currency}, " +
                         $"Overage: {projectedBalance - limitValue:N2} {context.Currency}.",
                metadata: new Dictionary<string, object>
                {
                    ["CurrentBalance"] = currentBalance.Value,
                    ["Amount"] = context.Amount,
                    ["ProjectedBalance"] = projectedBalance,
                    ["CreditLimit"] = limitValue,
                    ["Overage"] = projectedBalance - limitValue,
                    ["Currency"] = context.Currency
                });
        }

        // Rule 2: Warning threshold (90% utilisation by default)
        var utilisation = limitValue > 0 ? projectedBalance / limitValue : 0;
        if (utilisation >= _warningThreshold)
        {
            return Warn(
                code: AccountingErrorCodes.CreditLimitWarning,
                message: $"Account '{context.AccountCode}' will reach " +
                         $"{utilisation:P1} of its credit limit " +
                         $"({projectedBalance:N2} / {limitValue:N2} {context.Currency}) " +
                         $"after this posting. Review account balance.");
        }

        return Pass();
    }
}
