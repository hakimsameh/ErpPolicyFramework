using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Sales.Policies.SalesInvoice;

/// <summary>
/// Ensures no line item would result in negative stock after the invoice is fulfilled.
/// Similar to inventory negative stock check, but in sales context.
/// </summary>
public sealed class NegativeStockOnInvoicePolicy : PolicyBase<SalesInvoiceContext>
{
    /// <summary>Policy name for bypass/config — use instead of hardcoded string.</summary>
    public const string Name = "Sales.Invoice.NegativeStock";
    public override string PolicyName => Name;
    public override int Order => PolicyOrderingConventions.BusinessRuleMin + 10;

    public override async Task<PolicyResult> EvaluateAsync(
        SalesInvoiceContext context,
        CancellationToken cancellationToken = default)
    {
        var violations = new List<PolicyViolation>();

        foreach (var line in context.LineItems)
        {
            var currentStock = await context.GetStockForItem(line.ItemCode, line.WarehouseCode);
            var resultingStock = currentStock - line.Quantity; // Sale reduces stock

            if (resultingStock < 0)
            {
                violations.Add(new PolicyViolation(
                    SalesErrorCodes.NegativeStock,
                    $"Sale would result in negative stock for item '{line.ItemCode}' in warehouse '{line.WarehouseCode}'. " +
                    $"Current: {currentStock:N2}, Sale qty: {line.Quantity:N2}, Resulting: {resultingStock:N2} {line.UnitOfMeasure}.",
                    PolicySeverity.Error,
                    Field: nameof(SalesInvoiceLineItem.Quantity),
                    Metadata: new Dictionary<string, object>
                    {
                        ["ItemCode"]         = line.ItemCode,
                        ["WarehouseCode"]    = line.WarehouseCode,
                        ["CurrentStock"]     = currentStock,
                        ["Quantity"]         = line.Quantity,
                        ["ResultingStock"]   = resultingStock
                    }));
            }
        }

        return violations.Count > 0 ? FailMany(violations) : Pass();
    }
}
