# API at a Glance — Quick Lookup

All public types you'll use. For examples, see [QUICK_REFERENCE.md](QUICK_REFERENCE.md).

---

## Core Types

| Type | Purpose |
|------|---------|
| `IPolicyExecutor<TContext>` | Inject this. Call `ExecuteAsync(context)` to run all policies. |
| `IPolicyContext` | Marker. Your context (e.g. `InventoryAdjustmentContext`) implements this. |
| `IPolicy<TContext>` | Policy contract. Extend `PolicyBase<TContext>` to implement. |
| `AggregatedPolicyResult` | Returned by `ExecuteAsync`. Has `IsSuccess`, `BlockingViolations`, `AdvisoryViolations`. |
| `PolicyViolation` | One violation: `Code`, `Message`, `Severity`, `Field`, `Metadata`. |
| `PolicyExecutionOptions` | Options for a single run: `Strategy`, `ThrowOnFailure`, `BypassedPolicies`, etc. |
| `PolicyViolationException` | Thrown when `ThrowOnFailure` is true and pipeline fails. Has `AggregatedResult`. |

---

## Context Types (pre-built)

| Type | Module | Use for |
|------|--------|---------|
| `InventoryAdjustmentContext` | Inventory | Stock adjustments |
| `PostingContext` | Posting | Journal entries, invoices |
| `AccountAssignmentContext` | Accounting | GL account assignment |

---

## DI Extension Methods

| Method | Purpose |
|--------|---------|
| `AddPolicyFramework()` | Registers the executor. Call first. |
| `AddPoliciesFromAssemblies(Assembly...)` | Auto-registers all policies from assemblies. |
| `AddPoliciesFromAssemblies(lifetime, excludeTypes, Assembly...)` | Same, but exclude some policies. |
| `AddPolicy<TContext>(factory)` | Register one policy with a factory (for configurable params). |
| `AddPolicy<TContext, TPolicy>()` | Register one policy by type. |
| `AddPolicyFrameworkWithConfiguration(config)` | Full setup with appsettings (Host-style). |

---

## Enums & Constants

| Type | Values |
|------|--------|
| `ExecutionStrategy` | `CollectAll`, `FailFast` |
| `PolicySeverity` | `Info`, `Warning`, `Error`, `Critical` |
| `PolicyOrderingConventions` | `HardGateMin/Max`, `BusinessRuleMin/Max`, `CrossModuleMin/Max`, `AdvisoryMin/Max` |

---

## Result Properties (AggregatedPolicyResult)

| Property | Type | Meaning |
|----------|------|---------|
| `IsSuccess` | bool | True if no blocking violations |
| `IsFailure` | bool | Inverse of IsSuccess |
| `BlockingViolations` | `IReadOnlyList<PolicyViolation>` | Errors + Critical |
| `AdvisoryViolations` | `IReadOnlyList<PolicyViolation>` | Warnings + Info |
| `AllViolations` | `IReadOnlyList<PolicyViolation>` | Everything |
| `PoliciesEvaluated` | int | How many ran |
| `PoliciesFailed` | int | How many had blocking violations |
| `ContextTypeName` | string | Name of context type |
| `ExecutedAt` | DateTimeOffset | When pipeline finished |

---

## Violation Properties (PolicyViolation)

| Property | Type | Meaning |
|----------|------|---------|
| `Code` | string | e.g. "INV-001" |
| `Message` | string | Human-readable |
| `Severity` | PolicySeverity | Info, Warning, Error, Critical |
| `Field` | string? | Context property (for UI highlighting) |
| `Metadata` | `IDictionary<string,object>?` | Extra data |
