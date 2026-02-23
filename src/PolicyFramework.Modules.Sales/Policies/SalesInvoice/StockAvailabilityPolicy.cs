using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Sales.Policies.SalesInvoice;

/// <summary>
/// Ensures each line item has sufficient stock available before allowing the invoice.
/// </summary>
public sealed class StockAvailabilityPolicy : PolicyBase<SalesInvoiceContext>
{
    public override string PolicyName => "Sales.Invoice.StockAvailability";
    public override int Order => PolicyOrderingConventions.BusinessRuleMin + 5;

    public override async Task<PolicyResult> EvaluateAsync(
        SalesInvoiceContext context,
        CancellationToken cancellationToken = default)
    {
        var violations = new List<PolicyViolation>();

        foreach (var line in context.LineItems)
        {
            var currentStock = await context.GetStockForItem(line.ItemCode, line.WarehouseCode);

            if (currentStock < line.Quantity)
            {
                violations.Add(new PolicyViolation(
                    SalesErrorCodes.ItemNotAvailable,
                    $"Item '{line.ItemCode}' has insufficient stock in warehouse '{line.WarehouseCode}'. " +
                    $"Required: {line.Quantity:N2} {line.UnitOfMeasure}, Available: {currentStock:N2}.",
                    PolicySeverity.Error,
                    Field: nameof(SalesInvoiceLineItem.Quantity),
                    Metadata: new Dictionary<string, object>
                    {
                        ["ItemCode"]      = line.ItemCode,
                        ["WarehouseCode"] = line.WarehouseCode,
                        ["Required"]      = line.Quantity,
                        ["Available"]     = currentStock
                    }));
            }
        }

        return violations.Count > 0 ? FailMany(violations) : Pass();
    }
}
