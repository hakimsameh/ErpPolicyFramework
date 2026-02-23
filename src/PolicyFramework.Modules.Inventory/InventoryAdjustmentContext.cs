using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Inventory;

/// <summary>
/// Policy evaluation context for inventory adjustment transactions.
/// Carries all data that inventory policies need for their evaluation.
/// This is a pure data-carrier (value object) — zero business logic.
/// </summary>
public sealed class InventoryAdjustmentContext : IPolicyContext
{
    /// <summary>Warehouse location code (e.g. "WH-EAST").</summary>
    public required string WarehouseCode { get; init; }

    /// <summary>Item/SKU identifier being adjusted.</summary>
    public required string ItemCode { get; init; }

    /// <summary>Unit of measure (e.g. "EA", "KG", "L").</summary>
    public required string UnitOfMeasure { get; init; }

    /// <summary>
    /// Quantity to adjust. Negative = stock reduction; positive = stock increase.
    /// </summary>
    public required decimal AdjustmentQuantity { get; init; }

    /// <summary>Current on-hand stock before this adjustment.</summary>
    public required Func<Task<decimal>> CurrentStock { get; init; }

    /// <summary>
    /// Quantity threshold that triggers a replenishment signal.
    /// Zero means no reorder point is configured.
    /// </summary>
    public required Func<Task<decimal>> ReorderPoint { get; init; }

    /// <summary>
    /// Maximum allowable on-hand quantity.
    /// Zero means no maximum is configured.
    /// </summary>
    public required Func<Task<decimal>> MaxStockLevel { get; init; }

    /// <summary>
    /// Reason code for the adjustment (e.g. "CYCLE_COUNT", "WRITE_OFF", "SHRINKAGE").
    /// Required for large negative adjustments under SOX audit controls.
    /// </summary>
    public required string AdjustmentReason { get; init; }

    /// <summary>Identity of the user or system requesting the adjustment.</summary>
    public required string RequestedBy { get; init; }

    /// <summary>Business date of the transaction.</summary>
    public required DateTimeOffset TransactionDate { get; init; }

    // -------------------------------------------------------------------------
    // Computed properties — derived from the raw fields above
    // -------------------------------------------------------------------------

    /// <summary>True when this adjustment removes stock.</summary>
    public bool IsNegativeAdjustment => AdjustmentQuantity < 0;
}
