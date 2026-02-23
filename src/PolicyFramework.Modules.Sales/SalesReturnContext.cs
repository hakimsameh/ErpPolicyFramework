using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Sales;

/// <summary>
/// Policy context for Sales Return creation.
/// Use <see cref="IPolicyExecutor{SalesReturnContext}"/> to run return-specific policies.
/// </summary>
public sealed class SalesReturnContext : IPolicyContext
{
    /// <summary>Customer identifier.</summary>
    public required string CustomerCode { get; init; }

    /// <summary>Original sales invoice/document id for traceability.</summary>
    public required string OriginalSaleDocumentId { get; init; }

    /// <summary>Date of the original sale (for return period check).</summary>
    public required DateTimeOffset OriginalSaleDate { get; init; }

    /// <summary>Return document date.</summary>
    public required DateTimeOffset ReturnDate { get; init; }

    /// <summary>Line items being returned.</summary>
    public required IReadOnlyList<SalesReturnLineItem> LineItems { get; init; }

    /// <summary>User or system creating the return.</summary>
    public required string CreatedBy { get; init; }

    // --- Data providers (lazy, async) ---

    /// <summary>Maximum days between sale and return. Exceeding blocks the return.</summary>
    public required int MaxReturnPeriodDays { get; init; }

    /// <summary>Check if customer bought this item on the original sale (saleDocId, itemCode) → true if yes.</summary>
    public required Func<string, string, Task<bool>> CustomerBoughtItemOnSale { get; init; }

    /// <summary>Check if the product is returnable (policy, product flag, etc.).</summary>
    public required Func<string, Task<bool>> IsProductReturnable { get; init; }
}

/// <summary>One line item on a sales return.</summary>
public sealed record SalesReturnLineItem(
    string ItemCode,
    decimal Quantity
);
