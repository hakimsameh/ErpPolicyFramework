using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Sales.Policies.SalesInvoice;

/// <summary>
/// When invoice is credit (not cash), ensures customer does not exceed credit limit.
/// Skips when IsCredit is false.
/// </summary>
public sealed class CustomerCreditLimitPolicy : PolicyBase<SalesInvoiceContext>
{
    public override string PolicyName => "Sales.Invoice.CreditLimit";
    public override int Order => PolicyOrderingConventions.BusinessRuleMin;

    public override async Task<PolicyResult> EvaluateAsync(
        SalesInvoiceContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.IsCredit)
            return Pass();

        if (context.CreditLimit is null || context.CurrentBalance is null)
            return Pass(); // No credit data → skip

        var limit = await context.CreditLimit();
        var balance = await context.CurrentBalance();
        if (limit is null || balance is null)
            return Pass();

        var projectedBalance = balance.Value + context.DocumentTotal;
        if (projectedBalance <= limit.Value)
            return Pass();

        return Fail(
            code: SalesErrorCodes.CreditLimitExceeded,
            message: $"Credit sale would exceed customer '{context.CustomerCode}' credit limit. " +
                     $"Projected balance: {projectedBalance:N2} {context.Currency}, " +
                     $"Limit: {limit:N2} {context.Currency}.",
            field: nameof(context.DocumentTotal),
            metadata: new Dictionary<string, object>
            {
                ["CustomerCode"]     = context.CustomerCode,
                ["DocumentTotal"]    = context.DocumentTotal,
                ["CurrentBalance"]   = balance.Value,
                ["CreditLimit"]      = limit.Value,
                ["ProjectedBalance"] = projectedBalance
            });
    }
}
