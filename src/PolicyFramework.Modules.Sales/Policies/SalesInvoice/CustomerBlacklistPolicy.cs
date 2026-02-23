using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Sales.Policies.SalesInvoice;

/// <summary>
/// Blocks invoice when customer is on the blacklist.
/// </summary>
public sealed class CustomerBlacklistPolicy : PolicyBase<SalesInvoiceContext>
{
    public override string PolicyName => "Sales.Invoice.CustomerBlacklist";
    public override int Order => PolicyOrderingConventions.HardGateMin + 1;

    public override async Task<PolicyResult> EvaluateAsync(
        SalesInvoiceContext context,
        CancellationToken cancellationToken = default)
    {
        var isBlacklisted = await context.IsCustomerBlacklisted();
        if (isBlacklisted)
        {
            return Fail(
                code: SalesErrorCodes.CustomerBlacklisted,
                message: $"Customer '{context.CustomerCode}' is on the blacklist. Sales are not allowed.",
                field: nameof(context.CustomerCode),
                metadata: new Dictionary<string, object> { ["CustomerCode"] = context.CustomerCode });
        }

        return Pass();
    }
}
