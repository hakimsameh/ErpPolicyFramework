using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Posting;

/// <summary>Type of GL document being posted.</summary>
public enum PostingDocumentType
{
    SalesInvoice,
    PurchaseInvoice,
    JournalEntry,
    PaymentReceipt,
    CreditNote,
    DebitNote
}

/// <summary>Status of the target fiscal period.</summary>
public enum FiscalPeriodStatus
{
    /// <summary>Accepting new postings normally.</summary>
    Open,

    /// <summary>Month-end procedures are in progress; warn on new postings.</summary>
    Closing,

    /// <summary>Period is closed; no new postings allowed.</summary>
    Closed,

    /// <summary>Period is locked by finance administration; no changes whatsoever.</summary>
    Locked
}

/// <summary>
/// Policy evaluation context for general ledger posting operations.
/// Encapsulates all data that posting policies require — no business logic.
/// </summary>
public sealed class PostingContext : IPolicyContext
{
    /// <summary>Document number (e.g. "JE-2024-00512", "INV-2024-00099").</summary>
    public required string DocumentNumber { get; init; }

    /// <summary>Classification of the document being posted.</summary>
    public required PostingDocumentType DocumentType { get; init; }

    /// <summary>Company code within the ERP (e.g. "CORP-01").</summary>
    public required string CompanyCode { get; init; }

    /// <summary>Target general ledger (e.g. "LEDGER-GEN", "LEDGER-CONS").</summary>
    public required string LedgerCode { get; init; }

    /// <summary>Business date of the source document.</summary>
    public required DateTimeOffset DocumentDate { get; init; }

    /// <summary>Date the entry will appear in the general ledger.</summary>
    public required DateTimeOffset PostingDate { get; init; }

    /// <summary>Fiscal year of the target period.</summary>
    public required int FiscalYear { get; init; }

    /// <summary>Fiscal period number (1–12 for monthly, 1–4 for quarterly).</summary>
    public required int FiscalPeriod { get; init; }

    /// <summary>Current status of the target fiscal period.</summary>
    public required Func<Task<FiscalPeriodStatus>> PeriodStatus { get; init; }

    /// <summary>Sum of all debit line items on the document.</summary>
    public required decimal TotalDebit { get; init; }

    /// <summary>Sum of all credit line items on the document.</summary>
    public required decimal TotalCredit { get; init; }

    /// <summary>Transaction currency (ISO 4217 code, e.g. "USD", "EUR").</summary>
    public required string Currency { get; init; }

    /// <summary>Identity of the user or integration initiating the posting.</summary>
    public required string PostedBy { get; init; }

    /// <summary>True when this document involves an intercompany transaction.</summary>
    public bool IsIntercompany { get; init; }

    /// <summary>
    /// Partner company code for intercompany transactions.
    /// Must be set when <see cref="IsIntercompany"/> is true.
    /// </summary>
    public string? IntercompanyPartnerCode { get; init; }

    // -------------------------------------------------------------------------
    // Computed properties
    // -------------------------------------------------------------------------

    /// <summary>Absolute imbalance between debits and credits.</summary>
    public decimal Imbalance => Math.Abs(TotalDebit - TotalCredit);
}
