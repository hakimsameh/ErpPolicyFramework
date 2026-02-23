# Test Coverage — All Scenarios Audited

Complete audit of tested scenarios vs. available functionality.

---

## Test Summary

| Test File | Tests | Covers |
|-----------|-------|--------|
| PolicyExecutorTests.cs | 18 | Executor: ordering, disabled, CollectAll, FailFast, resilience, ThrowOnFailure, parallel, empty pipeline |
| DependencyInjectionTests.cs | 12 | DI: assembly scan, executor resolution, manual registration, full pipeline, idempotency |
| InventoryPolicyTests.cs | ~20 | NegativeStock, AdjustmentReason, MaxStockLevel, ReorderPointAlert |
| PostingPolicyTests.cs | ~20 | BalancedEntry, OpenFiscalPeriod, FutureDatePosting, IntercompanyPartner |
| AccountingPolicyTests.cs | ~18 | ActiveAccount, CostCenter, CreditLimit, DualControl, pipeline integration |
| SalesPolicyTests.cs | ~20 | Sales Invoice & Return: unit + integration |
| DemoScenariosIntegrationTests.cs | 11 | Each Host demo produces expected outcome |

---

## Demo Scenarios (11) — Expected Outcomes

| # | Demo | Expected | Key Violations |
|---|------|----------|----------------|
| 1 | InventoryHappyPath | ✓ PASS | — |
| 2 | InventoryMultipleViolations | ✗ FAIL | INV-001 (negative), INV-002 (reason) |
| 3 | InventoryReorderAlert | ✓ PASS | INV-W002 (advisory) |
| 4 | PostingUnbalanced | ✗ FAIL | PST-001 |
| 5 | PostingLockedPeriod | ✗ FAIL | PST-002 |
| 6 | PostingClosingPeriodIntercompany | ✗ FAIL | PST-004 (same company), PST-W001 (warning) |
| 7 | AccountingBlockedAccount | ✗ FAIL | ACC-001 |
| 8 | AccountingCreditBreach | ✗ FAIL | ACC-004, ACC-W001 |
| 9 | StrategyFailFast | ✗ FAIL | Stops at first (INV-002 or INV-001) |
| 10 | StrategyParallelTiers | ✗ FAIL | ACC-003 (cost center) |
| 11 | ResilienceFaultingPolicy | ✗ FAIL | POLICY_EXCEPTION (Critical) |

---

## Policy-by-Policy Coverage

### Inventory
| Policy | Unit Tests | Integration |
|--------|------------|-------------|
| NegativeStockPolicy | ✓ 8 tests | ✓ FullPipeline |
| AdjustmentReasonMandatoryPolicy | ✓ 5 tests | ✓ |
| MaxStockLevelPolicy | ✓ 3 tests | ✓ |
| ReorderPointAlertPolicy | ✓ 4 tests | ✓ |

### Posting
| Policy | Unit Tests | Integration |
|--------|------------|-------------|
| BalancedEntryPolicy | ✓ 4 tests | ✓ FullPipeline |
| OpenFiscalPeriodPolicy | ✓ 4 tests | ✓ |
| FutureDatePostingPolicy | ✓ 4 tests | ✓ (manual reg) |
| IntercompanyPartnerValidationPolicy | ✓ 6 tests | ✓ |

### Accounting
| Policy | Unit Tests | Integration |
|--------|------------|-------------|
| ActiveAccountPolicy | ✓ 4 tests | ✓ FullPipeline |
| CostCenterMandatoryPolicy | ✓ 4 tests | ✓ |
| CreditLimitPolicy | ✓ 6 tests | ✓ |
| DualControlManualEntryPolicy | ✓ 3 tests | ✓ |

### Sales (Module)
| Policy | Unit Tests | Integration |
|--------|------------|-------------|
| CustomerCreditLimitPolicy | ✓ | ✓ |
| CustomerBlacklistPolicy | ✓ | ✓ |
| StockAvailabilityPolicy | ✓ | ✓ |
| NegativeStockOnInvoicePolicy | ✓ | ✓ |
| CustomerBoughtProductPolicy | ✓ | ✓ |
| ProductReturnablePolicy | ✓ | ✓ |
| ReturnPeriodPolicy | ✓ | ✓ |

---

## Execution Options Coverage

| Option | Tested |
|--------|--------|
| Strategy: CollectAll | ✓ |
| Strategy: FailFast | ✓ |
| ThrowOnFailure | ✓ |
| ParallelizeSameOrderTier | ✓ |
| BypassedPolicies | ✓ (added) |
| MaxDegreeOfParallelism | — (relies on default) |

---

## Run Tests

```bash
dotnet test ErpPolicyFramework.sln
```

For verbose: `dotnet test ErpPolicyFramework.sln --logger "console;verbosity=detailed"`
