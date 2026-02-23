# ERP Generic Policy Framework
### .NET 8 · C# 12 · DDD · Clean Architecture

---

## 🚀 New Here? Start Here

| I want to… | Go to |
|------------|-------|
| **Get started in 5 minutes** | [docs/GETTING_STARTED.md](docs/GETTING_STARTED.md) |
| **Copy-paste examples** for execution, options, contexts | [docs/QUICK_REFERENCE.md](docs/QUICK_REFERENCE.md) |
| **Do a specific thing** (validate, skip policy, add policy, etc.) | [docs/COMMON_TASKS.md](docs/COMMON_TASKS.md) |
| **Understand the pipeline** (CollectAll, FailFast, parallel, etc.) | [docs/PIPELINE_GUIDE.md](docs/PIPELINE_GUIDE.md) |
| **Look up an API** (types, methods, properties) | [docs/API_AT_A_GLANCE.md](docs/API_AT_A_GLANCE.md) |
| **Apply different policies per document type** (Invoice vs Return) | [docs/SALES_MODULE_GUIDE.md](docs/SALES_MODULE_GUIDE.md) |

All docs are written for junior developers — no prior policy-framework experience needed.

---

## Quick Start

**Prerequisites:** .NET 8 SDK (`dotnet --version` must show 8.x)

```bash
# Build
dotnet build ErpPolicyFramework.sln

# Run the demo (see all policies in action)
dotnet run --project src/PolicyFramework.Host

# Run tests
dotnet test ErpPolicyFramework.sln
```

**One command (build + test + demo):**  
- Windows: `build.cmd`  
- Linux/macOS: `./build.sh`

---

## Core Concepts (5 Things to Know)

| Concept | What it is |
|---------|------------|
| **Context** | Data object (e.g. `InventoryAdjustmentContext`) with everything a policy needs to validate |
| **Policy** | One business rule (e.g. "no negative stock"). Auto-registered from assemblies |
| **Executor** | `IPolicyExecutor<TContext>` — call `ExecuteAsync(context)` to run all policies |
| **Result** | `AggregatedPolicyResult` — `IsSuccess`, `BlockingViolations`, `AdvisoryViolations` |
| **Violation** | One failed rule: `Code`, `Message`, `Severity`, `Field` |

---

## Solution Structure

```
ErpPolicyFramework/
│
├── docs/                                     ← Documentation (start here if new)
│   ├── GETTING_STARTED.md                    ← Step-by-step for beginners
│   ├── QUICK_REFERENCE.md                    ← Copy-paste examples
│   ├── COMMON_TASKS.md                       ← How to do X (all functions)
│   ├── PIPELINE_GUIDE.md                     ← Pipeline execution (all scenarios + examples)
│   ├── API_AT_A_GLANCE.md                    ← Type/method quick lookup
│   └── SALES_MODULE_GUIDE.md                 ← Invoice vs Return policies (separate contexts)
│
├── .editorconfig                             ← Code style and formatting
├── Directory.Build.props                     ← Shared MSBuild properties
│
├── src/
│   ├── PolicyFramework.Core/                  ← Zero business logic; pure framework
│   │   ├── Abstractions/
│   │   │   ├── IPolicyContext.cs              ← Marker interface for all contexts
│   │   │   ├── IPolicy.cs                     ← Core policy contract (generic)
│   │   │   ├── PolicyBase.cs                  ← Optional convenience base class
│   │   │   ├── PolicyResult.cs                ← Immutable single-policy result
│   │   │   ├── PolicyViolation.cs             ← Violation record + PolicySeverity enum
│   │   │   ├── AggregatedPolicyResult.cs      ← Pipeline-level result + exception
│   │   │   ├── IPolicyExecutor.cs             ← Executor contract + options + strategy enum
│   │   │   └── PolicyOrderingConventions.cs   ← Order range constants (documentation)
│   │   ├── Execution/
│   │   │   └── PolicyExecutor.cs              ← Default pipeline executor
│   │   └── DependencyInjection/
│   │       └── PolicyFrameworkServiceExtensions.cs  ← AddPolicyFramework(), scanning
│   │
│   ├── PolicyFramework.Modules.Inventory/
│   │   ├── InventoryAdjustmentContext.cs
│   │   └── Policies/
│   │       ├── AdjustmentReasonMandatoryPolicy.cs   Order: 5
│   │       ├── NegativeStockPolicy.cs               Order: 10
│   │       ├── MaxStockLevelPolicy.cs               Order: 20
│   │       └── ReorderPointAlertPolicy.cs           Order: 30
│   │
│   ├── PolicyFramework.Modules.Posting/
│   │   ├── PostingContext.cs
│   │   └── Policies/
│   │       ├── BalancedEntryPolicy.cs               Order: 1
│   │       ├── OpenFiscalPeriodPolicy.cs            Order: 2
│   │       ├── FutureDatePostingPolicy.cs           Order: 5
│   │       └── IntercompanyPartnerValidationPolicy.cs Order: 10
│   │
│   ├── PolicyFramework.Modules.Sales/
│   │   ├── SalesInvoiceContext.cs
│   │   ├── SalesReturnContext.cs
│   │   └── Policies/
│   │       ├── SalesInvoice/   (CreditLimit, Blacklist, Stock, NegativeStock)
│   │       └── SalesReturn/    (BoughtProduct, Returnable, ReturnPeriod)
│   │
│   ├── PolicyFramework.Modules.Accounting/
│   │   ├── AccountAssignmentContext.cs
│   │   └── Policies/
│   │       ├── ActiveAccountPolicy.cs               Order: 1
│   │       ├── CostCenterMandatoryPolicy.cs         Order: 10
│   │       ├── CreditLimitPolicy.cs                 Order: 15
│   │       └── DualControlManualEntryPolicy.cs      Order: 20
│   │
│   └── PolicyFramework.Host/
│       ├── Program.cs                         ← Slim entry point
│       ├── appsettings.json                   ← Logging and policy configuration
│       ├── appsettings.Development.json       ← Development overrides
│       ├── Configuration/
│       │   ├── PolicyFrameworkHostOptions.cs   ← Configuration binding
│       │   └── PolicyFrameworkHostServiceExtensions.cs  ← DI with config
│       └── Demo/
│           ├── DemoRunner.cs                  ← Console output formatting
│           ├── DemoScenarios.cs               ← 11 predefined scenarios
│           └── FaultingInventoryPolicy.cs      ← Resilience demo helper
│
└── tests/
    └── PolicyFramework.Tests/
        ├── Core/
        │   ├── PolicyExecutorTests.cs         ← 18 executor unit tests
        │   └── DependencyInjectionTests.cs    ← 12 DI integration tests
        ├── Inventory/
        │   └── InventoryPolicyTests.cs        ← 20 policy unit tests
        ├── Posting/
        │   └── PostingPolicyTests.cs          ← 20 policy unit tests
        └── Accounting/
            └── AccountingPolicyTests.cs       ← 18 policy unit tests + pipeline test
```

---

## Configuration

Policy parameters can be configured via `appsettings.json`:

```json
{
  "PolicyFramework": {
    "FutureDatePostingMaxDays": 60,
    "CreditLimitWarningThreshold": 0.85,
    "AdjustmentReasonMandatoryThreshold": -50
  }
}
```

Logging levels are configurable per namespace; use `appsettings.Development.json` for debug output.

---

## How to Add a New Policy (Zero Framework Changes)

```csharp
// 1. Implement the policy — nothing else needed
public sealed class ProcurementBudgetPolicy : PolicyBase<ProcurementContext>
{
    public override string PolicyName => "Procurement.BudgetCheck";
    public override int    Order      => 15;  // Business Rule tier

    public override Task<PolicyResult> EvaluateAsync(
        ProcurementContext ctx, CancellationToken ct = default)
    {
        if (ctx.OrderValue > ctx.AvailableBudget)
            return Task.FromResult(Fail("PRC-001",
                $"Order value {ctx.OrderValue:C} exceeds available budget {ctx.AvailableBudget:C}."));

        return Task.FromResult(Pass());
    }
}

// 2. If the assembly is already in AddPoliciesFromAssemblies() → automatically registered.
//    If it's a new assembly → add one line in Program.cs:
services.AddPoliciesFromAssembly(typeof(ProcurementBudgetPolicy).Assembly);

// 3. Inject and execute anywhere in the application:
public class CreatePoCommandHandler(IPolicyExecutor<ProcurementContext> policies)
{
    public async Task HandleAsync(CreatePoCommand cmd, CancellationToken ct)
    {
        var ctx    = BuildContext(cmd);
        var result = await policies.ExecuteAsync(ctx, cancellationToken: ct);

        if (result.IsFailure)
            return Result.Fail(result.BlockingViolations.Select(v => v.Message));

        // advisory warnings available but don't block:
        foreach (var w in result.AdvisoryViolations)
            _notifier.Notify(w.Code, w.Message);

        // proceed with domain logic
    }
}
```

---

## Policy Ordering Conventions

| Range  | Tier Name     | Purpose                                    |
|--------|---------------|--------------------------------------------|
| 1–9    | Hard Gate     | Existence, format, fast prerequisite checks |
| 10–49  | Business Rule | Core domain invariants                      |
| 50–79  | Cross-Module  | Rules requiring multiple bounded contexts   |
| 80–99  | Advisory      | Informational signals, soft warnings        |
| 100+   | Default       | Unordered or module-specific               |

---

## Execution Strategies

| Strategy                 | Behaviour                                           | Best For                     |
|--------------------------|-----------------------------------------------------|------------------------------|
| `CollectAll` (default)   | Runs all policies; aggregates every violation       | UI validation, full reports  |
| `FailFast`               | Stops at first Error/Critical violation             | High-throughput pipelines    |
| `ParallelizeSameOrderTier`| Runs same-order policies concurrently              | Independent I/O-bound checks |

---

## Violation Severities

| Severity | Blocks Pipeline? | Use Case                                        |
|----------|------------------|-------------------------------------------------|
| Info     | No               | Downstream event triggers (e.g. raise PO)       |
| Warning  | No               | Advisory conditions (require acknowledgement)   |
| Error    | **Yes**          | Hard business rule violation                    |
| Critical | **Yes**          | System failure, infrastructure error            |

---

## CI/CD

| Workflow | Trigger | What it does |
|----------|---------|--------------|
| **CI** | Push / PR to `main` | Build + test |
| **CD** | Release published or tag `v*` pushed | Build, test, pack NuGet, upload artifacts; optionally publish to NuGet.org |

**To publish to NuGet.org:** Add `NUGET_API_KEY` in repo **Settings → Secrets** (see [docs/CI_CD.md](docs/CI_CD.md)). CD runs on tag push (`v1.0.0`) or release publish.

**Repository:** [https://github.com/hakimsameh/ErpPolicyFramework](https://github.com/hakimsameh/ErpPolicyFramework)
