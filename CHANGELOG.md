# Changelog

All notable changes to the ERP Generic Policy Framework are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.1.0] - 2025-02-24

### Added

- **Non-generic `IPolicyExecutor`** — Single injection point when a service handles multiple context types
- **`PolicyExecutorDispatcher`** — Delegates to `IPolicyExecutor<TContext>` based on context type at call site (no reflection)
- Benchmarks comparing Generic vs Non-Generic executor performance (`benchmarks/PolicyFramework.Benchmarks`)
- Documentation updates across README, GETTING_STARTED, QUICK_REFERENCE, API_AT_A_GLANCE, COMMON_TASKS, PIPELINE_GUIDE, SALES_MODULE_GUIDE

### Changed

- DI registration: `AddPolicyFramework()` now registers both `IPolicyExecutor<TContext>` and `IPolicyExecutor`
- `TryAddTransient` used for non-generic executor for idempotent `AddPolicyFramework()` calls

---

## [1.0.0] - 2024

### Added

- Core policy framework with `IPolicy<TContext>`, `IPolicyExecutor<TContext>`, `PolicyExecutor`
- Policy pipeline: ordering, disabled policies, FailFast, CollectAll, parallel tiers
- `PolicyResult`, `AggregatedPolicyResult`, `PolicyViolation`, `PolicyViolationException`
- `PolicyExecutionOptions`: Strategy, ThrowOnFailure, BypassedPolicies, ParallelizeSameOrderTier
- Module: Inventory (NegativeStock, MaxStockLevel, ReorderPointAlert, AdjustmentReasonMandatory)
- Module: Posting (BalancedEntry, OpenFiscalPeriod, FutureDatePosting, IntercompanyPartnerValidation)
- Module: Accounting (ActiveAccount, CostCenterMandatory, CreditLimit, DualControlManualEntry)
- Module: Sales (Invoice & Return policies)
- Assembly scanning for auto-registration (`AddPoliciesFromAssemblies`)
- Host demo with 11 predefined scenarios
