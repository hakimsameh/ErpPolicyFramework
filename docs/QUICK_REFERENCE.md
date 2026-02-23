# Quick Reference — Copy & Paste Examples

All common operations in one place. Replace placeholders with your data.

For a full pipeline guide with all scenarios, see [PIPELINE_GUIDE.md](PIPELINE_GUIDE.md).

---

## 1. Execute Policies (Basic)

```csharp
// Inject
public MyService(IPolicyExecutor<InventoryAdjustmentContext> _policies) { }

// Execute with default options (collects all violations)
var result = await _policies.ExecuteAsync(context);

if (result.IsSuccess)
    // Proceed
else
    foreach (var v in result.BlockingViolations)
        Console.WriteLine($"[{v.Code}] {v.Message}");
```

---

## 2. Execute with Options

```csharp
// Stop at first error (FailFast)
var result = await _policies.ExecuteAsync(context, new PolicyExecutionOptions
{
    Strategy       = ExecutionStrategy.FailFast,
    ThrowOnFailure = false
});

// Throw exception on failure instead of returning result
var result = await _policies.ExecuteAsync(context, new PolicyExecutionOptions
{
    ThrowOnFailure = true  // throws PolicyViolationException if any blocking violation
});

// Skip specific policies for this run
var result = await _policies.ExecuteAsync(context, new PolicyExecutionOptions
{
    BypassedPolicies = new HashSet<string> { "Inventory.NegativeStock" }
});

// Run same-order policies in parallel (for performance)
var result = await _policies.ExecuteAsync(context, new PolicyExecutionOptions
{
    ParallelizeSameOrderTier = true
});
```

---

## 3. Handle Result

```csharp
var result = await _policies.ExecuteAsync(context);

// Quick checks
bool passed = result.IsSuccess;           // true = no blocking violations
bool failed = result.IsFailure;           // inverse of IsSuccess

// Violations
var blocking   = result.BlockingViolations;   // Errors + Critical — these block the operation
var advisory   = result.AdvisoryViolations;   // Warnings + Info — operation can proceed
var all        = result.AllViolations;       // Everything

// Each violation has:
foreach (var v in result.BlockingViolations)
{
    string code    = v.Code;      // e.g. "INV-001"
    string message = v.Message;   // Human-readable
    string? field  = v.Field;     // Context property that failed (for UI)
    var metadata   = v.Metadata;  // Extra data (numbers, codes, etc.)
}

// Throw if failed (alternative to manual check)
result.ThrowIfFailed();  // throws PolicyViolationException with AggregatedResult
```

---

## 4. Build Contexts (Inventory, Posting, Accounting)

### Inventory Adjustment

```csharp
var context = new InventoryAdjustmentContext
{
    WarehouseCode      = "WH-01",
    ItemCode           = "ITEM-123",
    UnitOfMeasure      = "EA",
    AdjustmentQuantity = -50m,   // negative = reduction
    CurrentStock       = () => Task.FromResult(500m),     // or async repo call
    ReorderPoint       = () => Task.FromResult(100m),
    MaxStockLevel      = () => Task.FromResult(1000m),
    AdjustmentReason   = "CYCLE_COUNT",
    RequestedBy        = "user@company.com",
    TransactionDate    = DateTimeOffset.UtcNow
};
```

### Posting (Journal Entry)

```csharp
var context = new PostingContext
{
    DocumentNumber  = "JE-2024-001",
    DocumentType    = PostingDocumentType.JournalEntry,
    CompanyCode     = "CORP-01",
    LedgerCode      = "LEDGER-GEN",
    DocumentDate    = DateTimeOffset.UtcNow,
    PostingDate     = DateTimeOffset.UtcNow,
    FiscalYear      = DateTimeOffset.UtcNow.Year,
    FiscalPeriod    = DateTimeOffset.UtcNow.Month,
    PeriodStatus    = () => Task.FromResult(FiscalPeriodStatus.Open),
    TotalDebit      = 10_000m,
    TotalCredit     = 10_000m,
    Currency        = "USD",
    PostedBy        = "accountant@company.com"
};
```

### Account Assignment

```csharp
var context = new AccountAssignmentContext
{
    AccountCode        = "4100-SALES",
    AccountDescription = "Sales Revenue",
    AccountType        = AccountType.Revenue,
    AccountStatus      = AccountStatus.Active,
    CompanyCode        = "CORP-01",
    CostCenter         = "CC-SALES",
    Amount             = 25_000m,
    Currency           = "USD",
    DocumentType       = "SI",
    AssignedBy         = "system",
    RequiresCostCenter = true,
    IsManualEntry      = false
    // CreditLimit & CurrentBalance optional — only needed for credit-limit checks
};
```

---

## 5. DI Registration

### Minimal (auto-scan policies)

```csharp
builder.Services.AddPolicyFramework();
builder.Services.AddPoliciesFromAssemblies(
    typeof(NegativeStockPolicy).Assembly,
    typeof(BalancedEntryPolicy).Assembly,
    typeof(ActiveAccountPolicy).Assembly
);
```

### With configuration (Host-style)

```csharp
builder.Services.AddPolicyFrameworkWithConfiguration(builder.Configuration);
// Requires PolicyFrameworkHostServiceExtensions and appsettings "PolicyFramework" section
```

### Add a single policy with custom parameters

```csharp
builder.Services.AddPolicy<PostingContext>(
    _ => new FutureDatePostingPolicy(maxFutureDays: 90));
```

### Exclude policies from scan, register manually

```csharp
builder.Services.AddPoliciesFromAssemblies(
    ServiceLifetime.Transient,
    excludeTypes: new[] { typeof(CreditLimitPolicy) },
    typeof(ActiveAccountPolicy).Assembly
);
builder.Services.AddPolicy<AccountAssignmentContext>(
    _ => new CreditLimitPolicy(warningThreshold: 0.80m));
```

---

## 6. Policy Order Values (when writing a new policy)

| Range  | Use for                         | Example |
|--------|----------------------------------|---------|
| 1–9    | Hard gates (format, existence)  | Balanced entry, blocked account |
| 10–49  | Business rules                  | Negative stock, credit limit |
| 50–79  | Cross-module / external data    | Intercompany validation |
| 80–99  | Advisory (warnings, info)       | Reorder alert |

---

## 7. Violation Severities

| Severity | Blocks? | When to use |
|----------|---------|-------------|
| `Info`   | No      | Informational signal (e.g. "stock low") |
| `Warning`| No      | Advisory (e.g. "credit 90% used") |
| `Error`  | Yes     | Hard rule violated |
| `Critical`| Yes    | System/infrastructure failure |
