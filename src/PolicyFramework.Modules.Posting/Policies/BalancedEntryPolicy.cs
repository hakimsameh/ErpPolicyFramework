using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Posting.Policies;

/// <summary>
/// Enforces the double-entry bookkeeping invariant: Debits must equal Credits.
/// This is the most fundamental rule in accounting — no posting may proceed
/// with an imbalanced journal entry.
///
/// A small rounding tolerance (<see cref="BalanceTolerance"/>) accommodates
/// currency conversion rounding across multi-currency documents.
///
/// Order: 1 (first Hard Gate — the most fundamental check)
/// </summary>
public sealed class BalancedEntryPolicy : PolicyBase<PostingContext>
{
    /// <summary>Maximum allowable imbalance due to rounding. 0.01 currency unit.</summary>
    private const decimal BalanceTolerance = 0.01m;

    public override string PolicyName => "Posting.BalancedEntry";
    public override int Order => PolicyOrderingConventions.HardGateMin; // = 1

    public override Task<PolicyResult> EvaluateAsync(
        PostingContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Imbalance > BalanceTolerance)
        {
            return Task.FromResult(Fail(
                code: PostingErrorCodes.UnbalancedEntry,
                message: $"Document '{context.DocumentNumber}' is not balanced. " +
                         $"Debit={context.TotalDebit:N2} {context.Currency}, " +
                         $"Credit={context.TotalCredit:N2} {context.Currency}, " +
                         $"Imbalance={context.Imbalance:N4} {context.Currency} " +
                         $"(tolerance: {BalanceTolerance:N2}).",
                metadata: new Dictionary<string, object>
                {
                    ["TotalDebit"] = context.TotalDebit,
                    ["TotalCredit"] = context.TotalCredit,
                    ["Imbalance"] = context.Imbalance,
                    ["Tolerance"] = BalanceTolerance,
                    ["Currency"] = context.Currency
                }));
        }

        return Task.FromResult(Pass());
    }
}
