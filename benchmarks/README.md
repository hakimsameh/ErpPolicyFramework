# Policy Framework Benchmarks

Benchmarks comparing **Generic** `IPolicyExecutor<TContext>` vs **Non-Generic** `IPolicyExecutor` (PolicyExecutorDispatcher) performance.

## Run

```bash
# All benchmarks (Generic vs Non-Generic for SinglePolicy, WithOptions, FivePolicies)
dotnet run -c Release --project benchmarks/PolicyFramework.Benchmarks

# Quick run (shorter iterations)
dotnet run -c Release --project benchmarks/PolicyFramework.Benchmarks -- --job short

# Filter by category
dotnet run -c Release --project benchmarks/PolicyFramework.Benchmarks -- --filter "*FivePolicies*"
```

## Scenarios

| Benchmark | Generic | Non-Generic | Purpose |
|-----------|---------|-------------|---------|
| FivePolicies | `IPolicyExecutor<T>.ExecuteAsync(ctx)` | `IPolicyExecutor.ExecuteAsync(ctx)` | Baseline — 5 trivial policies |
| FivePolicies_WithOptions | Same + `PolicyExecutionOptions` | Same | Options allocation per call |

## Expected Results

The **Non-Generic** path adds one `GetRequiredService<IPolicyExecutor<TContext>>` per call. The overhead is typically **&lt; 1 µs** for in-process DI resolution. For typical business workflows (validation before save), this is negligible.

## Output

Results show Mean time (µs), Memory (allocations), and Ratio vs baseline (Generic).
