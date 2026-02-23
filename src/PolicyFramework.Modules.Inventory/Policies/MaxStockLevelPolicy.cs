using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Inventory.Policies;

/// <summary>
/// Warns when an inbound adjustment pushes stock above the configured maximum.
///
/// Business rule: Overstocking ties up working capital and warehouse space.
/// This policy signals the condition but does not block the transaction
/// (the planner decides whether to proceed).
///
/// Severity: Warning (advisory — IsSuccess remains true)
/// Order: 20 (Business Rule tier)
/// </summary>
public sealed class MaxStockLevelPolicy : PolicyBase<InventoryAdjustmentContext>
{
    /// <summary>Policy name for bypass/config — use instead of hardcoded string.</summary>
    public const string Name = "Inventory.MaxStockLevel";
    public override string PolicyName => Name;
    public override int Order => PolicyOrderingConventions.BusinessRuleMin + 10; // = 20

    public override async Task<PolicyResult> EvaluateAsync(
        InventoryAdjustmentContext context,
        CancellationToken cancellationToken = default)
    {
        var maxStockLevel = await context.MaxStockLevel();
        // Skip when no maximum is configured (MaxStockLevel == 0)
        if (maxStockLevel <= 0)
            return Pass();

        var currentStock = await context.CurrentStock();
        var resultingStock = currentStock + context.AdjustmentQuantity;

        if (resultingStock > maxStockLevel)
        {
            return Warn(
                code: InventoryErrorCodes.MaxStockLevelExceeded,
                message: $"Resulting stock ({resultingStock:N2} {context.UnitOfMeasure}) " +
                         $"exceeds the maximum stock level " +
                         $"({maxStockLevel:N2} {context.UnitOfMeasure}) " +
                         $"for item '{context.ItemCode}' in warehouse '{context.WarehouseCode}'. " +
                         $"Consider cancelling or reducing the inbound quantity.");
        }

        return Pass();
    }
}
