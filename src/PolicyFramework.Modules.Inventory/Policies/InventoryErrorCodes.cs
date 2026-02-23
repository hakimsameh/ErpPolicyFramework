namespace PolicyFramework.Modules.Inventory.Policies;

/// <summary>
/// Centralised error codes for Inventory policies.
/// </summary>
public static class InventoryErrorCodes
{
    public const string NegativeStock = "INV-001";
    public const string ReasonCodeMandatory = "INV-002";

    public const string MaxStockLevelExceeded = "INV-W001";
    public const string ReorderPointReached = "INV-W002";
}
