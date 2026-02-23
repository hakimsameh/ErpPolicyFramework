using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Inventory.Policies;

/// <summary>
/// Emits an informational signal when a negative adjustment causes stock
/// to fall at or below the configured reorder point.
///
/// Business rule: This signal triggers downstream purchase-order creation workflows.
/// The policy does NOT block the transaction — it is purely advisory.
/// Only fires when stock crosses the threshold (was above, will be at/below).
///
/// Severity: Warning (advisory)
/// Order: 30 (Business Rule tier — runs after NegativeStock so ResultingStock is valid)
/// </summary>
public sealed class ReorderPointAlertPolicy : PolicyBase<InventoryAdjustmentContext>
{
    public override string PolicyName => "Inventory.ReorderPointAlert";
    public override int Order => PolicyOrderingConventions.BusinessRuleMin + 20; // = 30

    public override async Task<PolicyResult> EvaluateAsync(
        InventoryAdjustmentContext context,
        CancellationToken cancellationToken = default)
    {
        var reorderPoint = await context.ReorderPoint();
        // Skip when no reorder point is configured
        if (reorderPoint <= 0)
            return Pass();

        var currentStock = await context.CurrentStock();
        var resultingStock = currentStock + context.AdjustmentQuantity;

        // Only alert when the adjustment causes the threshold to be crossed (not already below)
        bool willCrossThreshold = currentStock > reorderPoint
                               && resultingStock <= reorderPoint;

        if (willCrossThreshold)
        {
            return Warn(
                code: InventoryErrorCodes.ReorderPointReached,
                message: $"Stock for item '{context.ItemCode}' in warehouse '{context.WarehouseCode}' " +
                         $"will reach {resultingStock:N2} {context.UnitOfMeasure} — " +
                         $"at or below the reorder point of {reorderPoint:N2} {context.UnitOfMeasure}. " +
                         $"Consider raising a purchase order.");
        }

        return Pass();
    }
}
