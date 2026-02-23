using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Accounting;

/// <summary>Classification of a GL account per the chart of accounts.</summary>
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense,
    Intercompany
}

/// <summary>Operational status of a GL account.</summary>
public enum AccountStatus
{
    /// <summary>Accepting postings normally.</summary>
    Active,

    /// <summary>Account is temporarily inactive — postings not allowed.</summary>
    Inactive,

    /// <summary>Account is administratively blocked — postings strictly forbidden.</summary>
    Blocked
}

/// <summary>
/// Policy evaluation context for GL account assignment validation.
/// Used when assigning an account to a document line item.
/// Pure data-carrier — no business logic.
/// </summary>
public sealed class AccountAssignmentContext : IPolicyContext
{
    /// <summary>GL account code (e.g. "4100-SALES", "2100-AP").</summary>
    public required string AccountCode { get; init; }

    /// <summary>Human-readable account name.</summary>
    public required string AccountDescription { get; init; }

    /// <summary>Account classification in the chart of accounts.</summary>
    public required AccountType AccountType { get; init; }

    /// <summary>Current operational status of the account.</summary>
    public required AccountStatus AccountStatus { get; init; }

    /// <summary>Company code this account belongs to.</summary>
    public required string CompanyCode { get; init; }

    /// <summary>Cost center assigned to this line item (may be empty).</summary>
    public required string CostCenter { get; init; }

    /// <summary>Amount being posted to this account (positive = debit, negative = credit).</summary>
    public required decimal Amount { get; init; }

    /// <summary>Transaction currency (ISO 4217).</summary>
    public required string Currency { get; init; }

    /// <summary>Source document type (e.g. "SI", "PI", "JE").</summary>
    public required string DocumentType { get; init; }

    /// <summary>Identity of the user or system assigning this account.</summary>
    public required string AssignedBy { get; init; }

    /// <summary>
    /// When true, a cost center is mandatory for this account.
    /// Typically true for Expense and Revenue accounts.
    /// </summary>
    public bool RequiresCostCenter { get; init; }

    /// <summary>
    /// When true, this is a manually entered journal entry
    /// (as opposed to a system-generated posting).
    /// Triggers segregation-of-duties checks.
    /// </summary>
    public bool IsManualEntry { get; init; }

    /// <summary>
    /// Maximum allowable balance for this account.
    /// Optional — when null or returns null, credit limit checks are skipped.
    /// </summary>
    public Func<Task<decimal?>>? CreditLimit { get; init; }

    /// <summary>
    /// Current account balance before this posting.
    /// Optional — required only when credit limit validation applies.
    /// </summary>
    public Func<Task<decimal?>>? CurrentBalance { get; init; }
}
