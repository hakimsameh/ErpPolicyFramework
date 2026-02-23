# Common Tasks — How to Do Everything

Step-by-step guides for every function. For junior developers.

For a complete pipeline walkthrough with all execution scenarios and examples, see [PIPELINE_GUIDE.md](PIPELINE_GUIDE.md).

---

## Table of Contents

1. [Execute policies in my API/Service](#1-execute-policies-in-my-apiservice)
2. [Validate before saving to database](#2-validate-before-saving-to-database)
3. [Show all validation errors to the user (form validation)](#3-show-all-validation-errors-to-the-user-form-validation)
4. [Stop at first error (FailFast)](#4-stop-at-first-error-failfast)
5. [Throw exception on validation failure](#5-throw-exception-on-validation-failure)
6. [Skip a policy for one request](#6-skip-a-policy-for-one-request)
7. [Add a new policy (custom rule)](#7-add-a-new-policy-custom-rule)
8. [Add a policy with configurable parameters](#8-add-a-policy-with-configurable-parameters)
9. [Configure policy thresholds (appsettings)](#9-configure-policy-thresholds-appsettings)
10. [Handle warnings (non-blocking violations)](#10-handle-warnings-non-blocking-violations)
11. [Get field-level errors for UI](#11-get-field-level-errors-for-ui)
12. [Run policies in parallel (performance)](#12-run-policies-in-parallel-performance)
13. [Disable a policy entirely](#13-disable-a-policy-entirely)
14. [Test my code that uses policies](#14-test-my-code-that-uses-policies)
15. [Different policies per document type (Invoice vs Return)](#15-different-policies-per-document-type-invoice-vs-return)
16. [Use internal modifiers for policies](#16-use-internal-modifiers-for-policies)

---

## 1. Execute policies in my API/Service

**Goal:** Call the policy framework from your controller, service, or command handler.

**Steps:**

1. Inject `IPolicyExecutor<TContext>` where TContext matches your domain (e.g. `InventoryAdjustmentContext`).
2. Build a context object with your request data.
3. Call `ExecuteAsync(context)`.
4. Check `result.IsSuccess` and handle violations.

**Example:**

```csharp
[ApiController]
[Route("api/inventory")]
public class InventoryController(IPolicyExecutor<InventoryAdjustmentContext> _policies) : ControllerBase
{
    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust([FromBody] AdjustRequest req)
    {
        var context = new InventoryAdjustmentContext
        {
            WarehouseCode      = req.WarehouseCode,
            ItemCode           = req.ItemCode,
            AdjustmentQuantity = req.Quantity,
            CurrentStock       = () => _repo.GetStockAsync(req.ItemCode, req.WarehouseCode),
            ReorderPoint       = () => _repo.GetReorderPointAsync(req.ItemCode),
            MaxStockLevel      = () => _repo.GetMaxStockAsync(req.ItemCode),
            AdjustmentReason   = req.ReasonCode,
            RequestedBy        = User.Identity!.Name!,
            TransactionDate   = DateTimeOffset.UtcNow,
            UnitOfMeasure      = req.Uom ?? "EA"
        };

        var result = await _policies.ExecuteAsync(context);

        if (result.IsFailure)
            return BadRequest(result.BlockingViolations.Select(v => new { v.Code, v.Message }));

        await _repo.ApplyAdjustmentAsync(req);
        return Ok();
    }
}
```

---

## 2. Validate before saving to database

**Goal:** Run policies first; only save if all pass.

**Pattern:**

```csharp
var result = await _policies.ExecuteAsync(context);

if (result.IsFailure)
    return Result.Fail(result.BlockingViolations.Select(v => v.Message));

// All policies passed — safe to save
await _db.SaveChangesAsync();
```

---

## 3. Show all validation errors to the user (form validation)

**Goal:** Collect every violation so the user sees a full list (e.g. "Fix these 3 errors").

**Use default strategy `CollectAll`** (this is the default; no options needed):

```csharp
var result = await _policies.ExecuteAsync(context);
// All policies run; result.BlockingViolations has everything
var errors = result.BlockingViolations.Select(v => v.Message).ToList();
```

---

## 4. Stop at first error (FailFast)

**Goal:** Stop as soon as one policy fails (e.g. for performance).

```csharp
var result = await _policies.ExecuteAsync(context, new PolicyExecutionOptions
{
    Strategy = ExecutionStrategy.FailFast
});
// result.BlockingViolations will have at most one violation (the first)
```

---

## 5. Throw exception on validation failure

**Goal:** Use try/catch instead of `if (result.IsFailure)`.

```csharp
var result = await _policies.ExecuteAsync(context, new PolicyExecutionOptions
{
    ThrowOnFailure = true
});

// If we get here, no blocking violations
// If any blocking violation, PolicyViolationException is thrown

// Or manually:
result.ThrowIfFailed();
```

**Catch it:**

```csharp
try
{
    var result = await _policies.ExecuteAsync(context, new PolicyExecutionOptions { ThrowOnFailure = true });
    // proceed
}
catch (PolicyViolationException ex)
{
    var violations = ex.AggregatedResult.BlockingViolations;
    return BadRequest(violations);
}
```

---

## 6. Skip a policy for one request

**Goal:** Bypass specific policies for a single execution (e.g. admin override).

```csharp
var result = await _policies.ExecuteAsync(context, new PolicyExecutionOptions
{
    BypassedPolicies = new HashSet<string> { "Inventory.NegativeStock", "Posting.BalancedEntry" }
});
```

---

## 7. Add a new policy (custom rule)

**Goal:** Add your own validation rule that runs automatically.

**Steps:**

1. Create a context type (or use existing) implementing `IPolicyContext`.
2. Create a policy class extending `PolicyBase<TContext>`.
3. Implement `PolicyName`, `Order`, and `EvaluateAsync`.
4. Add the assembly to `AddPoliciesFromAssemblies` (or it's already there).

**Example:**

```csharp
public sealed class MinimumOrderValuePolicy : PolicyBase<OrderContext>
{
    public override string PolicyName => "Order.MinimumValue";
    public override int Order => 15;  // Business Rule tier

    public override Task<PolicyResult> EvaluateAsync(
        OrderContext context, CancellationToken ct = default)
    {
        if (context.TotalAmount < 10m)
            return Task.FromResult(Fail("ORD-001", "Minimum order value is 10.00"));
        return Task.FromResult(Pass());
    }
}
```

No other code changes — it's auto-discovered.

---

## 8. Add a policy with configurable parameters

**Goal:** Policy has parameters (e.g. threshold) that differ per environment.

**Register with a factory:**

```csharp
builder.Services.AddPolicy<PostingContext>(
    _ => new FutureDatePostingPolicy(maxFutureDays: 90));
```

**Or from configuration:**

```csharp
var maxDays = builder.Configuration.GetValue<int>("PolicyFramework:FutureDatePostingMaxDays");
builder.Services.AddPolicy<PostingContext>(
    _ => new FutureDatePostingPolicy(maxFutureDays: maxDays));
```

---

## 9. Configure policy thresholds (appsettings)

**Goal:** Change thresholds without recompiling.

In `appsettings.json`:

```json
{
  "PolicyFramework": {
    "FutureDatePostingMaxDays": 60,
    "CreditLimitWarningThreshold": 0.85,
    "AdjustmentReasonMandatoryThreshold": -50
  }
}
```

Then use `AddPolicyFrameworkWithConfiguration` (see Host project) or bind and register manually.

---

## 10. Handle warnings (non-blocking violations)

**Goal:** Log or notify about warnings but allow the operation.

```csharp
var result = await _policies.ExecuteAsync(context);

if (result.IsFailure)
    return BadRequest(result.BlockingViolations);

// Check advisory (warnings/info)
foreach (var v in result.AdvisoryViolations)
{
    if (v.Severity == PolicySeverity.Warning)
        _logger.LogWarning("[{Code}] {Message}", v.Code, v.Message);
    else if (v.Severity == PolicySeverity.Info)
        _eventBus.Publish(new PolicyInfoEvent(v.Code, v.Message));
}

// Proceed
```

---

## 11. Get field-level errors for UI

**Goal:** Highlight which form field caused the error.

```csharp
var result = await _policies.ExecuteAsync(context);

var fieldErrors = result.BlockingViolations
    .Where(v => v.Field is not null)
    .ToDictionary(v => v.Field!, v => v.Message);

// e.g. { "CostCenter": "Cost center is required", "AdjustmentQuantity": "..." }
```

---

## 12. Run policies in parallel (performance)

**Goal:** Policies in the same Order tier run concurrently.

```csharp
var result = await _policies.ExecuteAsync(context, new PolicyExecutionOptions
{
    ParallelizeSameOrderTier = true
});
```

**Caution:** Only use when policies in the same tier don't share writable state.

---

## 13. Disable a policy entirely

**Goal:** Turn off a policy (e.g. feature flag) without removing it.

Override `IsEnabled` in your policy:

```csharp
public override bool IsEnabled => _featureFlags.IsEnabled("NegativeStockCheck");
```

Or implement a wrapper that reads from configuration. Policies with `IsEnabled == false` are skipped.

---

## 14. Test my code that uses policies

**Goal:** Unit test your service that calls `IPolicyExecutor`.

**Option A — Use real executor with test policies:**

```csharp
var services = new ServiceCollection()
    .AddPolicyFramework()
    .AddPoliciesFromAssembly(typeof(NegativeStockPolicy).Assembly)
    .BuildServiceProvider();

var executor = services.GetRequiredService<IPolicyExecutor<InventoryAdjustmentContext>>();
var result = await executor.ExecuteAsync(context);
Assert.True(result.IsSuccess);
```

**Option B — Mock the executor:**

```csharp
var mockExecutor = new Mock<IPolicyExecutor<InventoryAdjustmentContext>>();
// Build a real executor with test policies, run once, capture result
var successResult = await BuildExecutor().ExecuteAsync(validContext);
mockExecutor
    .Setup(x => x.ExecuteAsync(It.IsAny<InventoryAdjustmentContext>(), null, default))
    .ReturnsAsync(successResult);

var service = new InventoryService(mockExecutor.Object);
```

---

## 15. Different policies per document type (Invoice vs Return)

**Goal:** Sales Invoice runs one set of policies (credit limit, stock, blacklist); Sales Return runs another (bought product, returnable, return period).

**Solution:** Use **separate contexts** and **separate executors**. Each document type has its own context → only its policies run.

```csharp
// Inject both executors
public SalesController(
    IPolicyExecutor<SalesInvoiceContext> _invoicePolicies,
    IPolicyExecutor<SalesReturnContext> _returnPolicies)
{
}

// For invoice creation
[HttpPost("invoice")]
public async Task<IActionResult> CreateInvoice(...)
{
    var context = new SalesInvoiceContext { ... };
    var result = await _invoicePolicies.ExecuteAsync(context);
    // Only invoice policies run (credit, stock, blacklist)
}

// For return creation
[HttpPost("return")]
public async Task<IActionResult> CreateReturn(...)
{
    var context = new SalesReturnContext { ... };
    var result = await _returnPolicies.ExecuteAsync(context);
    // Only return policies run (bought product, returnable, period)
}
```

**Full guide:** [SALES_MODULE_GUIDE.md](SALES_MODULE_GUIDE.md)

---

## 16. Use internal modifiers for policies

**Goal:** Keep policies as `internal` so they're not part of the module's public API.

**Solution:** `AddPoliciesFromAssemblies` already discovers internal types via reflection. The requirement is that the **host assembly** (the one that builds the DI container) must be able to instantiate them.

Add `InternalsVisibleTo` to each **module** project, granting access to your host and test assemblies:

```xml
<!-- In each module .csproj (Inventory, Posting, Accounting, Sales) -->
<ItemGroup>
  <InternalsVisibleTo Include="PolicyFramework.Host" />
  <InternalsVisibleTo Include="PolicyFramework.Tests" />
  <InternalsVisibleTo Include="DynamicProxyGenAssembly2" />   <!-- for Moq -->
</ItemGroup>
```

For your own host project (e.g. `MyCompany.ErpApi`), add:

```xml
<InternalsVisibleTo Include="MyCompany.ErpApi" />
```

Then you can use `internal` on policy classes:

```csharp
internal sealed class NegativeStockPolicy : PolicyBase<InventoryAdjustmentContext>
{
    // ...
}
```

---

## Built-in Policies Reference

| Module    | Policy                    | Code      | What it checks                          |
|-----------|---------------------------|-----------|----------------------------------------|
| Inventory | AdjustmentReasonMandatory | INV-002   | Reason required for large adjustments  |
| Inventory | NegativeStock             | INV-001   | No negative stock                      |
| Inventory | MaxStockLevel             | INV-003   | Don't exceed max                       |
| Inventory | ReorderPointAlert         | INV-I001  | Info when below reorder point          |
| Posting   | BalancedEntry             | PST-001   | Debits = Credits                        |
| Posting   | OpenFiscalPeriod          | PST-002   | Period not locked                       |
| Posting   | FutureDatePosting         | PST-003   | No far-future dates                     |
| Posting   | IntercompanyPartner       | PST-004   | Valid intercompany partner              |
| Accounting| ActiveAccount             | ACC-001/2 | Account not blocked/inactive            |
| Accounting| CostCenterMandatory       | ACC-003   | Cost center when required               |
| Accounting| CreditLimit               | ACC-004   | Within credit limit                     |
| Accounting| DualControlManualEntry    | ACC-W001  | Warning for manual entry to sensitive accounts |
