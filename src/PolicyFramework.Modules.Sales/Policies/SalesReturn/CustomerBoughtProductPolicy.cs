using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Sales.Policies.SalesReturn;

/// <summary>
/// Ensures the customer actually bought each returned item on the original sale.
/// </summary>
public sealed class CustomerBoughtProductPolicy : PolicyBase<SalesReturnContext>
{
    /// <summary>Policy name for bypass/config — use instead of hardcoded string.</summary>
    public const string Name = "Sales.Return.CustomerBoughtProduct";
    public override string PolicyName => Name;
    public override int Order => PolicyOrderingConventions.BusinessRuleMin;

    public override async Task<PolicyResult> EvaluateAsync(
        SalesReturnContext context,
        CancellationToken cancellationToken = default)
    {
        var violations = new List<PolicyViolation>();

        foreach (var line in context.LineItems)
        {
            var bought = await context.CustomerBoughtItemOnSale(context.OriginalSaleDocumentId, line.ItemCode);

            if (!bought)
            {
                violations.Add(new PolicyViolation(
                    SalesErrorCodes.CustomerDidNotBuy,
                    $"Customer '{context.CustomerCode}' did not purchase item '{line.ItemCode}' on the original sale. " +
                    $"Cannot return.",
                    PolicySeverity.Error,
                    Field: nameof(SalesReturnLineItem.ItemCode),
                    Metadata: new Dictionary<string, object>
                    {
                        ["CustomerCode"]         = context.CustomerCode,
                        ["ItemCode"]             = line.ItemCode,
                        ["OriginalSaleDocument"] = context.OriginalSaleDocumentId
                    }));
            }
        }

        return violations.Count > 0 ? FailMany(violations) : Pass();
    }
}
