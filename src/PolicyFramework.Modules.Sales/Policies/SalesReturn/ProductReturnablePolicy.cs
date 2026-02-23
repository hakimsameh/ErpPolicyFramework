using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Sales.Policies.SalesReturn;

/// <summary>
/// Ensures each product is eligible for return (e.g. not consumable, not final sale).
/// </summary>
public sealed class ProductReturnablePolicy : PolicyBase<SalesReturnContext>
{
    public override string PolicyName => "Sales.Return.ProductReturnable";
    public override int Order => PolicyOrderingConventions.BusinessRuleMin + 5;

    public override async Task<PolicyResult> EvaluateAsync(
        SalesReturnContext context,
        CancellationToken cancellationToken = default)
    {
        var violations = new List<PolicyViolation>();

        foreach (var line in context.LineItems)
        {
            var returnable = await context.IsProductReturnable(line.ItemCode);

            if (!returnable)
            {
                violations.Add(new PolicyViolation(
                    SalesErrorCodes.ProductNotReturnable,
                    $"Product '{line.ItemCode}' is not eligible for return (final sale, consumable, or policy restricted).",
                    PolicySeverity.Error,
                    Field: nameof(SalesReturnLineItem.ItemCode),
                    Metadata: new Dictionary<string, object>
                    {
                        ["ItemCode"] = line.ItemCode
                    }));
            }
        }

        return violations.Count > 0 ? FailMany(violations) : Pass();
    }
}
