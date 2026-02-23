# Getting Started — For Junior Developers

> **New to this framework?** Follow these steps. No prior policy-framework experience needed.

---

## Step 1: Understand What This Does (30 seconds)

The Policy Framework **validates business rules** before you save data. Example:

- ❌ Block a stock adjustment that would go negative
- ❌ Block a journal entry where debits ≠ credits
- ✅ Allow the operation only if all rules pass

You call one method → the framework runs all relevant rules → you get a pass/fail + list of violations.

---

## Step 2: Run the Demo (2 minutes)

```bash
# From the solution folder
dotnet run --project src/PolicyFramework.Host
```

You'll see 11 demos. Each shows:
- **✓ PASS** = all policies allowed it
- **✗ FAIL** = at least one policy blocked it, with violation codes (e.g. `INV-001`, `ACC-004`)

This is how the framework behaves in your app.

---

## Step 3: Add the Framework to Your Project

### 3a. Install packages

Add a reference to the Core project (or NuGet package when published):

```xml
<ProjectReference Include="path\to\PolicyFramework.Core\PolicyFramework.Core.csproj" />
<ProjectReference Include="path\to\PolicyFramework.Modules.Inventory\PolicyFramework.Modules.Inventory.csproj" />
<!-- Add other modules as needed: Posting, Accounting -->
```

### 3b. Register in `Program.cs` (or `Startup.cs`)

```csharp
// Minimal setup — auto-discovers all policies from the assemblies
builder.Services.AddPolicyFramework();
builder.Services.AddPoliciesFromAssemblies(
    typeof(NegativeStockPolicy).Assembly,   // Inventory
    typeof(BalancedEntryPolicy).Assembly,   // Posting
    typeof(ActiveAccountPolicy).Assembly    // Accounting
);
```

### 3c. Use it in your code

```csharp
// Inject the executor (one per context type)
public class InventoryService(IPolicyExecutor<InventoryAdjustmentContext> _policies)
{
    public async Task<Result> AdjustStockAsync(AdjustStockCommand cmd)
    {
        // 1. Build the context with your data
        var context = new InventoryAdjustmentContext
        {
            WarehouseCode      = cmd.WarehouseCode,
            ItemCode           = cmd.ItemCode,
            AdjustmentQuantity = cmd.Quantity,
            CurrentStock       = () => _stockRepo.GetCurrentAsync(cmd.ItemCode, cmd.Warehouse),
            ReorderPoint       = () => _stockRepo.GetReorderPointAsync(cmd.ItemCode),
            MaxStockLevel      = () => _stockRepo.GetMaxStockAsync(cmd.ItemCode),
            AdjustmentReason   = cmd.ReasonCode,
            RequestedBy        = cmd.UserId,
            TransactionDate   = DateTimeOffset.UtcNow,
            UnitOfMeasure      = "EA"
        };

        // 2. Run all policies
        var result = await _policies.ExecuteAsync(context);

        // 3. Check result
        if (result.IsFailure)
        {
            var errors = result.BlockingViolations
                .Select(v => v.Message)
                .ToList();
            return Result.Fail(errors);
        }

        // 4. Optional: handle warnings (e.g. reorder alert)
        foreach (var w in result.AdvisoryViolations)
            _logger.LogWarning("[{Code}] {Message}", w.Code, w.Message);

        // 5. Proceed with your business logic
        await _stockRepo.AdjustAsync(cmd);
        return Result.Ok();
    }
}
```

---

## Step 4: The Five Things You Need to Know

| # | Concept | What it means |
|---|---------|----------------|
| 1 | **Context** | A data object (e.g. `InventoryAdjustmentContext`) holding everything policies need to validate |
| 2 | **Policy** | One rule (e.g. "no negative stock"). Policies are auto-registered; you rarely touch them |
| 3 | **Executor** | `IPolicyExecutor<TContext>` — you call `ExecuteAsync(context)` and get a result |
| 4 | **Result** | `AggregatedPolicyResult` — `IsSuccess`, `BlockingViolations`, `AdvisoryViolations` |
| 5 | **Violation** | One failed rule: `Code`, `Message`, `Severity`, `Field` |

---

## Next Steps

- **Quick reference:** [QUICK_REFERENCE.md](QUICK_REFERENCE.md) — copy-paste examples for every function
- **Common tasks:** [COMMON_TASKS.md](COMMON_TASKS.md) — how-to guides (14 tasks covered)
- **API lookup:** [API_AT_A_GLANCE.md](API_AT_A_GLANCE.md) — all types and properties
- **Full README:** [../README.md](../README.md) — architecture and advanced usage
