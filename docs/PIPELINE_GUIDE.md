# Pipeline Guide — How Policy Execution Works

Complete guide to the policy pipeline with examples for all scenarios.

---

## Table of Contents

1. [How the Pipeline Works](#1-how-the-pipeline-works)
2. [Scenario 1: Default (CollectAll)](#2-scenario-1-default-collectall)
3. [Scenario 2: FailFast](#3-scenario-2-failfast)
4. [Scenario 3: ThrowOnFailure](#4-scenario-3-throwonfailure)
5. [Scenario 4: BypassedPolicies](#5-scenario-4-bypassedpolicies)
6. [Scenario 5: ParallelizeSameOrderTier](#6-scenario-5-parallelizesameordertier)
7. [Scenario 6: MaxDegreeOfParallelism](#7-scenario-6-maxdegreeofparallelism)
8. [Scenario 7: Combined Options](#8-scenario-7-combined-options)
9. [Scenario 8: Warnings (Advisory Violations)](#9-scenario-8-warnings-advisory-violations)
10. [Scenario 9: Disabled Policies](#10-scenario-9-disabled-policies)
11. [Scenario 10: Different Contexts = Different Pipelines](#11-scenario-10-different-contexts--different-pipelines)
12. [Quick Reference — All Options](#12-quick-reference--all-options)

---

## 1. How the Pipeline Works

The policy executor runs a **pipeline** of policies for a given context type.

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Context (e.g. InventoryAdjustmentContext)                              │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  PIPELINE EXECUTION                                                      │
│                                                                          │
│  1. Resolve all IPolicy<TContext> from DI                                │
│  2. Filter by IsEnabled (skip disabled)                                  │
│  3. Remove BypassedPolicies (if specified)                              │
│  4. Sort by Order ascending                                             │
│  5. Execute (sequential or parallel per tier)                           │
│  6. Aggregate results → AggregatedPolicyResult                          │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│  AggregatedPolicyResult                                                  │
│  • IsSuccess / IsFailure                                                 │
│  • BlockingViolations (Error, Critical)                                  │
│  • AdvisoryViolations (Warning, Info)                                    │
│  • PoliciesEvaluated                                                    │
└─────────────────────────────────────────────────────────────────────────┘
```

**Order tiers** (policies with same `Order` run in the same tier):

| Order Range | Tier        | Example Policies                    |
|-------------|-------------|-------------------------------------|
| 1–9         | Hard Gate   | BalancedEntry, ActiveAccount        |
| 10–49       | Business    | NegativeStock, CreditLimit          |
| 50–79       | Cross-Module | Intercompany validation             |
| 80–99       | Advisory    | ReorderPointAlert                   |

---

## 2. Scenario 1: Default (CollectAll)

**Behaviour:** All enabled policies run in Order sequence. Every violation is collected.

```csharp
// Minimal setup
var services = new ServiceCollection()
    .AddLogging()
    .AddPolicyFramework()
    .AddPoliciesFromAssemblies(
        typeof(NegativeStockPolicy).Assembly,
        typeof(BalancedEntryPolicy).Assembly,
        typeof(ActiveAccountPolicy).Assembly);
var sp = services.BuildServiceProvider();

var executor = sp.GetRequiredService<IPolicyExecutor<InventoryAdjustmentContext>>();

var context = new InventoryAdjustmentContext
{
    WarehouseCode      = "WH-01",
    ItemCode          = "ITEM-999",
    UnitOfMeasure     = "EA",
    AdjustmentQuantity = -600m,   // would go negative (stock=500)
    CurrentStock      = () => Task.FromResult(500m),
    ReorderPoint      = () => Task.FromResult(100m),
    MaxStockLevel     = () => Task.FromResult(1000m),
    AdjustmentReason  = "",      // missing (required for large adjustment)
    RequestedBy       = "user@corp.local",
    TransactionDate   = DateTimeOffset.UtcNow
};

// Default: no options = CollectAll
var result = await executor.ExecuteAsync(context);

Console.WriteLine($"Success: {result.IsSuccess}");           // false
Console.WriteLine($"Policies run: {result.PoliciesEvaluated}"); // all 4
Console.WriteLine($"Blocking: {result.BlockingViolations.Count}"); // 2 (INV-001, INV-002)

foreach (var v in result.BlockingViolations)
    Console.WriteLine($"  [{v.Code}] {v.Message}");
//  [INV-002] A reason code is required for adjustments...
//  [INV-001] Adjustment would result in negative stock...
```

**Use when:** Form validation, batch processing, or when you need a complete list of issues.

---

## 3. Scenario 2: FailFast

**Behaviour:** Stops at the first blocking violation. Fewer policies run.

```csharp
var result = await executor.ExecuteAsync(context, new PolicyExecutionOptions
{
    Strategy = ExecutionStrategy.FailFast
});

// Pipeline stops after first Error/Critical
Console.WriteLine($"Policies run: {result.PoliciesEvaluated}");  // e.g. 2 (stops early)
Console.WriteLine($"Blocking: {result.BlockingViolations.Count}"); // 1 (first only)

// Policies run in Order: AdjustmentReasonMandatory (5) runs before NegativeStock (10)
// So first failure is INV-002; pipeline stops; NegativeStock never runs
```

**Use when:** High throughput, quick rejection, or when the first error is enough.

---

## 4. Scenario 3: ThrowOnFailure

**Behaviour:** Throws `PolicyViolationException` when any blocking violation occurs.

```csharp
try
{
    var result = await executor.ExecuteAsync(context, new PolicyExecutionOptions
    {
        ThrowOnFailure = true
    });
    // If we reach here → all passed
    await SaveAdjustmentAsync(context);
}
catch (PolicyViolationException ex)
{
    var violations = ex.AggregatedResult.BlockingViolations;
    return BadRequest(new { errors = violations.Select(v => new { v.Code, v.Message }) });
}
```

**Or use `ThrowIfFailed()` on the result:**

```csharp
var result = await executor.ExecuteAsync(context);
result.ThrowIfFailed();  // throws PolicyViolationException if IsFailure
```

**Use when:** You prefer exception-based flow (middleware, global handler).

---

## 5. Scenario 4: BypassedPolicies

**Behaviour:** Skips named policies for this execution only.

**Use policy `Name` constants** — type-safe, refactor-safe (no magic strings):

```csharp
using PolicyFramework.Modules.Inventory.Policies;

var result = await executor.ExecuteAsync(context, new PolicyExecutionOptions
{
    BypassedPolicies = new HashSet<string>
    {
        NegativeStockPolicy.Name,
        AdjustmentReasonMandatoryPolicy.Name
    }
});

// Those policies never run; others (MaxStockLevel, ReorderPointAlert) still run
Console.WriteLine($"Policies run: {result.PoliciesEvaluated}");  // fewer
```

Each policy defines `public const string Name` (e.g. `NegativeStockPolicy.Name` = `"Inventory.NegativeStock"`).

**Use when:** Admin override, emergency bypass, or feature-specific exclusion.

---

## 6. Scenario 5: ParallelizeSameOrderTier

**Behaviour:** Policies with the same `Order` run concurrently. Different tiers remain sequential.

```csharp
var result = await executor.ExecuteAsync(context, new PolicyExecutionOptions
{
    ParallelizeSameOrderTier = true
});

// e.g. If NegativeStockPolicy and another policy both have Order=10,
// they run in parallel. Policies with Order 5, 10, 20 run as tiers:
// Tier 5 → Tier 10 (parallel within) → Tier 20
```

**Use when:** Policies in the same tier are independent (no shared writable state).

---

## 7. Scenario 6: MaxDegreeOfParallelism

**Behaviour:** Limits concurrent policies when `ParallelizeSameOrderTier` is true.

```csharp
var result = await executor.ExecuteAsync(context, new PolicyExecutionOptions
{
    ParallelizeSameOrderTier = true,
    MaxDegreeOfParallelism  = 2   // cap concurrency
});
```

**Use when:** Avoiding resource exhaustion with many policies in one tier.

---

## 8. Scenario 7: Combined Options

**Behaviour:** Options can be combined. Common patterns:

```csharp
// FailFast + ThrowOnFailure: stop early and throw
var result = await executor.ExecuteAsync(context, new PolicyExecutionOptions
{
    Strategy       = ExecutionStrategy.FailFast,
    ThrowOnFailure = true
});

// Bypass + CollectAll: skip some, collect rest (use policy Name)
// using PolicyFramework.Modules.Inventory.Policies;
var result = await executor.ExecuteAsync(context, new PolicyExecutionOptions
{
    BypassedPolicies = new HashSet<string> { MaxStockLevelPolicy.Name },
    Strategy         = ExecutionStrategy.CollectAll
});

// Parallel + CollectAll: run tiers in parallel, collect all violations
var result = await executor.ExecuteAsync(context, new PolicyExecutionOptions
{
    ParallelizeSameOrderTier = true,
    Strategy                 = ExecutionStrategy.CollectAll
});
```

---

## 9. Scenario 8: Warnings (Advisory Violations)

**Behaviour:** Warnings and Info do not block. `IsSuccess` stays true. Violations appear in `AdvisoryViolations`.

```csharp
var context = new InventoryAdjustmentContext
{
    // ... valid data, but stock drops below reorder point
    AdjustmentQuantity = -30m,
    CurrentStock       = () => Task.FromResult(200m),
    ReorderPoint       = () => Task.FromResult(180m),
    // ... other fields valid
};

var result = await executor.ExecuteAsync(context);

Console.WriteLine($"Success: {result.IsSuccess}");  // true (warning doesn't block)
Console.WriteLine($"Advisory: {result.AdvisoryViolations.Count}");  // 1

foreach (var v in result.AdvisoryViolations)
    Console.WriteLine($"  [WARN] {v.Code}: {v.Message}");
//  [WARN] INV-W002: Stock below reorder point...
```

**Use when:** Soft validations, audit trails, or downstream notifications.

---

## 10. Scenario 9: Disabled Policies

**Behaviour:** Policies with `IsEnabled == false` are never run.

```csharp
// In policy implementation:
public override bool IsEnabled => _config.GetValue<bool>("PolicyFramework:EnableNegativeStockCheck");
```

Or hardcoded for testing:

```csharp
public override bool IsEnabled => false;  // always skipped
```

Disabled policies do not appear in `PoliciesEvaluated`. No need for `BypassedPolicies` if you disable at registration/config level.

---

## 11. Scenario 10: Different Contexts = Different Pipelines

**Behaviour:** Each context type has its own pipeline. `IPolicyExecutor<SalesInvoiceContext>` runs invoice policies only; `IPolicyExecutor<SalesReturnContext>` runs return policies only.

```csharp
// Invoice pipeline
var invoiceExecutor = sp.GetRequiredService<IPolicyExecutor<SalesInvoiceContext>>();
var invoiceResult   = await invoiceExecutor.ExecuteAsync(invoiceContext);

// Return pipeline (completely separate)
var returnExecutor = sp.GetRequiredService<IPolicyExecutor<SalesReturnContext>>();
var returnResult   = await returnExecutor.ExecuteAsync(returnContext);
```

**Context → Pipeline mapping:**

| Context                   | Pipeline policies                                   |
|---------------------------|-----------------------------------------------------|
| `InventoryAdjustmentContext` | NegativeStock, AdjustmentReason, MaxStock, ReorderPoint |
| `PostingContext`          | BalancedEntry, OpenFiscalPeriod, FutureDate, Intercompany |
| `AccountAssignmentContext`| ActiveAccount, CostCenter, CreditLimit, DualControl |
| `SalesInvoiceContext`    | CustomerBlacklist, CreditLimit, Stock, NegativeStock |
| `SalesReturnContext`     | ReturnPeriod, CustomerBoughtProduct, ProductReturnable |

---

## 12. Quick Reference — All Options

| Option | Type | Default | Effect |
|--------|------|---------|--------|
| `Strategy` | `ExecutionStrategy` | `CollectAll` | `CollectAll` = run all; `FailFast` = stop at first blocking |
| `ThrowOnFailure` | `bool` | `false` | Throw `PolicyViolationException` when pipeline fails |
| `ParallelizeSameOrderTier` | `bool` | `false` | Run same-Order policies concurrently |
| `MaxDegreeOfParallelism` | `int` | `Environment.ProcessorCount` | Max concurrency when parallelizing |
| `BypassedPolicies` | `IReadOnlySet<string>?` | `null` | Policy names to skip — use `PolicyName.Name` (e.g. `NegativeStockPolicy.Name`) |

**Execution flow summary:**

```
ExecuteAsync(context, options)
    → Resolve policies
    → Filter IsEnabled, BypassedPolicies
    → Sort by Order
    → Execute (sequential or parallel tiers)
    → Aggregate
    → [If ThrowOnFailure && IsFailure] throw PolicyViolationException
    → Return AggregatedPolicyResult
```

---

## See Also

- [COMMON_TASKS.md](COMMON_TASKS.md) — Task-specific how-tos (FailFast, bypass, parallel, etc.)
- [QUICK_REFERENCE.md](QUICK_REFERENCE.md) — Copy-paste examples
- [API_AT_A_GLANCE.md](API_AT_A_GLANCE.md) — Type and method lookup
