using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Inventory.Policies;

/// <summary>
/// Prevents any adjustment that would result in negative on-hand stock.
///
/// Business rule: On-hand stock cannot fall below zero unless the warehouse
/// is explicitly configured for backorder (not modelled in this context).
///
/// Order: 10 (Business Rule tier — runs after prerequisite checks)
/// </summary>
public sealed class NegativeStockPolicy : PolicyBase<InventoryAdjustmentContext>
{
    /// <summary>Policy name for bypass/config — use instead of hardcoded string.</summary>
    public const string Name = "Inventory.NegativeStock";
    public override string PolicyName => Name;
    public override int Order => PolicyOrderingConventions.BusinessRuleMin; // = 10

    public override async Task<PolicyResult> EvaluateAsync(
        InventoryAdjustmentContext context,
        CancellationToken cancellationToken = default)
    {
        var currentStock = await context.CurrentStock();
        var resultingStock = currentStock + context.AdjustmentQuantity;

        if (resultingStock < 0)
        {
            return Fail(
                code: InventoryErrorCodes.NegativeStock,
                message: $"Adjustment would result in negative stock " +
                         $"({resultingStock:N2} {context.UnitOfMeasure}) " +
                         $"for item '{context.ItemCode}' in warehouse '{context.WarehouseCode}'. " +
                         $"Current stock: {currentStock:N2}, " +
                         $"Adjustment: {context.AdjustmentQuantity:N2}.",
                field: nameof(context.AdjustmentQuantity),
                metadata: new Dictionary<string, object>
                {
                    ["CurrentStock"] = currentStock,
                    ["AdjustmentQuantity"] = context.AdjustmentQuantity,
                    ["ResultingStock"] = resultingStock,
                    ["ItemCode"] = context.ItemCode,
                    ["WarehouseCode"] = context.WarehouseCode
                });
        }

        return Pass();
    }
}
