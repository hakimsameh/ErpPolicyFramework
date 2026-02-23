using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Sales;

/// <summary>
/// Policy context for Sales Invoice creation.
/// Use <see cref="IPolicyExecutor{SalesInvoiceContext}"/> to run invoice-specific policies.
/// </summary>
public sealed class SalesInvoiceContext : IPolicyContext
{
    /// <summary>Customer identifier.</summary>
    public required string CustomerCode { get; init; }

    /// <summary>When true, payment is deferred (credit sale); when false, cash sale.</summary>
    public required bool IsCredit { get; init; }

    /// <summary>Total invoice amount (for credit limit check when IsCredit).</summary>
    public required decimal DocumentTotal { get; init; }

    /// <summary>Currency code (e.g. "USD").</summary>
    public required string Currency { get; init; }

    /// <summary>Line items to validate for stock.</summary>
    public required IReadOnlyList<SalesInvoiceLineItem> LineItems { get; init; }

    /// <summary>Invoice document date.</summary>
    public required DateTimeOffset DocumentDate { get; init; }

    /// <summary>User or system creating the invoice.</summary>
    public required string CreatedBy { get; init; }

    // --- Data providers (lazy, async) ---

    /// <summary>Customer credit limit. Null = no limit or N/A. Only used when IsCredit.</summary>
    public Func<Task<decimal?>>? CreditLimit { get; init; }

    /// <summary>Customer current balance. Only used when IsCredit.</summary>
    public Func<Task<decimal?>>? CurrentBalance { get; init; }

    /// <summary>True when customer is on blacklist.</summary>
    public required Func<Task<bool>> IsCustomerBlacklisted { get; init; }

    /// <summary>Get current stock for an item in a warehouse.</summary>
    public required Func<string, string, Task<decimal>> GetStockForItem { get; init; }
}

/// <summary>One line item on a sales invoice.</summary>
public sealed record SalesInvoiceLineItem(
    string ItemCode,
    string WarehouseCode,
    string UnitOfMeasure,
    decimal Quantity
);
