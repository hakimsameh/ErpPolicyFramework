using Microsoft.Extensions.Logging.Abstractions;
using PolicyFramework.Core.Abstractions;
using PolicyFramework.Core.Execution;
using Xunit;

namespace PolicyFramework.Tests.Core;

// =============================================================================
// Test context and stub policies — zero external dependencies
// =============================================================================

public sealed class SampleContext : IPolicyContext
{
    public int Value { get; init; }
}

/// <summary>Always passes. Order = 1.</summary>
public sealed class AlwaysPassPolicy : PolicyBase<SampleContext>
{
    public override string PolicyName => "Test.AlwaysPass";
    public override int    Order      => 1;
    public override Task<PolicyResult> EvaluateAsync(SampleContext ctx, CancellationToken ct = default)
        => Task.FromResult(Pass());
}

/// <summary>Also passes. Order = 1 (same tier as AlwaysPass — for parallel/MaxDoP tests).</summary>
public sealed class AlsoPassPolicy : PolicyBase<SampleContext>
{
    public override string PolicyName => "Test.AlsoPass";
    public override int    Order      => 1;
    public override Task<PolicyResult> EvaluateAsync(SampleContext ctx, CancellationToken ct = default)
        => Task.FromResult(Pass());
}

/// <summary>Always fails with Error. Order = 2.</summary>
public sealed class AlwaysFailPolicy : PolicyBase<SampleContext>
{
    public override string PolicyName => "Test.AlwaysFail";
    public override int    Order      => 2;
    public override Task<PolicyResult> EvaluateAsync(SampleContext ctx, CancellationToken ct = default)
        => Task.FromResult(Fail("T-001", "Hard failure"));
}

/// <summary>Always emits a warning. Order = 3.</summary>
public sealed class AlwaysWarnPolicy : PolicyBase<SampleContext>
{
    public override string PolicyName => "Test.AlwaysWarn";
    public override int    Order      => 3;
    public override Task<PolicyResult> EvaluateAsync(SampleContext ctx, CancellationToken ct = default)
        => Task.FromResult(Warn("T-W001", "Advisory warning"));
}

/// <summary>Always disabled.</summary>
public sealed class AlwaysDisabledPolicy : PolicyBase<SampleContext>
{
    public override string PolicyName => "Test.AlwaysDisabled";
    public override int    Order      => 99;
    public override bool   IsEnabled  => false;
    public override Task<PolicyResult> EvaluateAsync(SampleContext ctx, CancellationToken ct = default)
        => Task.FromResult(Fail("T-999", "Should never run"));
}

/// <summary>Throws an unhandled exception — tests pipeline resilience.</summary>
public sealed class ThrowingPolicy : PolicyBase<SampleContext>
{
    public override string PolicyName => "Test.Throwing";
    public override int    Order      => 50;
    public override Task<PolicyResult> EvaluateAsync(SampleContext ctx, CancellationToken ct = default)
        => throw new InvalidOperationException("Simulated infrastructure failure");
}

// =============================================================================
// Helper factory
// =============================================================================

file static class ExecutorFactory
{
    public static PolicyExecutor<SampleContext> Create(params IPolicy<SampleContext>[] policies)
        => new(policies, NullLogger<PolicyExecutor<SampleContext>>.Instance);
}

// =============================================================================
// Tests
// =============================================================================

public sealed class PolicyExecutorTests
{
    // ── Ordering ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RunsPoliciesInAscendingOrder()
    {
        var executor = ExecutorFactory.Create(
            new AlwaysFailPolicy(),   // Order = 2 — registered first
            new AlwaysPassPolicy());  // Order = 1 — registered second

        var result = await executor.ExecuteAsync(new SampleContext());

        // Regardless of registration order, Order=1 must appear first
        Assert.Equal("Test.AlwaysPass", result.Results[0].PolicyName);
        Assert.Equal("Test.AlwaysFail", result.Results[1].PolicyName);
    }

    // ── Disabled policies ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SkipsDisabledPolicies()
    {
        var executor = ExecutorFactory.Create(
            new AlwaysPassPolicy(),
            new AlwaysDisabledPolicy());

        var result = await executor.ExecuteAsync(new SampleContext());

        Assert.Equal(1, result.PoliciesEvaluated);
        Assert.DoesNotContain(result.Results, r => r.PolicyName == "Test.AlwaysDisabled");
    }

    // ── CollectAll strategy ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CollectAll_RunsAllPoliciesEvenAfterFailure()
    {
        var executor = ExecutorFactory.Create(
            new AlwaysFailPolicy(),   // Order 2 — fails
            new AlwaysPassPolicy(),   // Order 1 — passes first
            new AlwaysWarnPolicy());  // Order 3 — should still run

        var result = await executor.ExecuteAsync(new SampleContext(),
            new PolicyExecutionOptions { Strategy = ExecutionStrategy.CollectAll });

        Assert.Equal(3, result.PoliciesEvaluated);
        Assert.True(result.IsFailure);
    }

    // ── FailFast strategy ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FailFast_StopsPipelineAfterFirstBlockingViolation()
    {
        var executor = ExecutorFactory.Create(
            new AlwaysPassPolicy(),   // Order 1 — passes
            new AlwaysFailPolicy(),   // Order 2 — fails → should stop here
            new AlwaysWarnPolicy());  // Order 3 — should NOT run

        var result = await executor.ExecuteAsync(new SampleContext(),
            new PolicyExecutionOptions { Strategy = ExecutionStrategy.FailFast });

        // AlwaysWarnPolicy (Order=3) must NOT have been evaluated
        Assert.Equal(2, result.PoliciesEvaluated);
        Assert.DoesNotContain(result.Results, r => r.PolicyName == "Test.AlwaysWarn");
    }

    // ── Aggregation ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AllPoliciesPass_ReturnsSuccess()
    {
        var executor = ExecutorFactory.Create(new AlwaysPassPolicy());
        var result   = await executor.ExecuteAsync(new SampleContext());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.AllViolations);
    }

    [Fact]
    public async Task ExecuteAsync_WarningsDoNotCauseFailure()
    {
        var executor = ExecutorFactory.Create(new AlwaysWarnPolicy());
        var result   = await executor.ExecuteAsync(new SampleContext());

        Assert.True(result.IsSuccess);
        Assert.Single(result.AdvisoryViolations);
        Assert.Empty(result.BlockingViolations);
    }

    [Fact]
    public async Task ExecuteAsync_BlockingViolationsAreCorrectlySeparated()
    {
        var executor = ExecutorFactory.Create(new AlwaysFailPolicy(), new AlwaysWarnPolicy());
        var result   = await executor.ExecuteAsync(new SampleContext());

        Assert.True(result.IsFailure);
        Assert.Single(result.BlockingViolations);
        Assert.Single(result.AdvisoryViolations);
        Assert.Equal(2, result.AllViolations.Count);
    }

    // ── ThrowOnFailure ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ThrowOnFailure_ThrowsPolicyViolationException()
    {
        var executor = ExecutorFactory.Create(new AlwaysFailPolicy());

        await Assert.ThrowsAsync<PolicyViolationException>(() =>
            executor.ExecuteAsync(new SampleContext(),
                new PolicyExecutionOptions { ThrowOnFailure = true }));
    }

    [Fact]
    public async Task ExecuteAsync_ThrowOnFailure_ExceptionCarriesAggregatedResult()
    {
        var executor = ExecutorFactory.Create(new AlwaysFailPolicy());

        var ex = await Assert.ThrowsAsync<PolicyViolationException>(() =>
            executor.ExecuteAsync(new SampleContext(),
                new PolicyExecutionOptions { ThrowOnFailure = true }));

        Assert.NotNull(ex.AggregatedResult);
        Assert.True(ex.AggregatedResult.IsFailure);
    }

    // ── Resilience ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FaultingPolicy_DoesNotCrashPipeline()
    {
        var executor = ExecutorFactory.Create(
            new AlwaysPassPolicy(),
            new ThrowingPolicy(),     // throws — must NOT propagate
            new AlwaysWarnPolicy());

        // Must complete without exception
        var result = await executor.ExecuteAsync(new SampleContext());

        Assert.True(result.IsFailure);
        Assert.Contains(result.AllViolations, v => v.Code == "POLICY_EXCEPTION");
        Assert.Contains(result.AllViolations, v => v.Severity == PolicySeverity.Critical);
    }

    [Fact]
    public async Task ExecuteAsync_FaultingPolicy_PipelineContinuesOtherPolicies_WhenCollectAll()
    {
        var executor = ExecutorFactory.Create(
            new ThrowingPolicy(),     // Order 50 — throws
            new AlwaysPassPolicy(),   // Order 1  — should still run
            new AlwaysWarnPolicy());  // Order 3  — should still run

        var result = await executor.ExecuteAsync(new SampleContext(),
            new PolicyExecutionOptions { Strategy = ExecutionStrategy.CollectAll });

        Assert.Equal(3, result.PoliciesEvaluated);
    }

    // ── AggregatedPolicyResult ────────────────────────────────────────────────

    [Fact]
    public async Task AggregatedResult_PoliciesEvaluated_IsCorrect()
    {
        var executor = ExecutorFactory.Create(
            new AlwaysPassPolicy(),
            new AlwaysFailPolicy(),
            new AlwaysWarnPolicy());

        var result = await executor.ExecuteAsync(new SampleContext());

        Assert.Equal(3, result.PoliciesEvaluated);
    }

    [Fact]
    public async Task AggregatedResult_PoliciesFailed_CountsOnlyBlockingFailures()
    {
        var executor = ExecutorFactory.Create(
            new AlwaysPassPolicy(),   // pass
            new AlwaysFailPolicy(),   // blocking failure
            new AlwaysWarnPolicy());  // warning — NOT counted as failure

        var result = await executor.ExecuteAsync(new SampleContext());

        Assert.Equal(1, result.PoliciesFailed);
    }

    // ── Parallel tier execution ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ParallelTier_ProducesSameResultsAsSequential()
    {
        var policies = new IPolicy<SampleContext>[]
        {
            new AlwaysPassPolicy(),
            new AlwaysFailPolicy(),
            new AlwaysWarnPolicy()
        };

        var sequential = ExecutorFactory.Create(policies);
        var parallel   = ExecutorFactory.Create(policies);

        var seqResult = await sequential.ExecuteAsync(new SampleContext(),
            new PolicyExecutionOptions { Strategy = ExecutionStrategy.CollectAll });

        var parResult = await parallel.ExecuteAsync(new SampleContext(),
            new PolicyExecutionOptions
            {
                Strategy                 = ExecutionStrategy.CollectAll,
                ParallelizeSameOrderTier = true
            });

        Assert.Equal(seqResult.IsSuccess,          parResult.IsSuccess);
        Assert.Equal(seqResult.PoliciesEvaluated,  parResult.PoliciesEvaluated);
        Assert.Equal(seqResult.BlockingViolations.Count, parResult.BlockingViolations.Count);
    }

    // ── BypassedPolicies ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_BypassedPolicies_SkipsSpecifiedPolicies()
    {
        var executor = ExecutorFactory.Create(
            new AlwaysPassPolicy(),
            new AlwaysFailPolicy());

        var result = await executor.ExecuteAsync(new SampleContext(), new PolicyExecutionOptions
        {
            BypassedPolicies = new HashSet<string> { "Test.AlwaysFail" }
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.PoliciesEvaluated);
        Assert.DoesNotContain(result.Results, r => r.PolicyName == "Test.AlwaysFail");
    }

    // ── Empty pipeline ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NoPoliciesRegistered_ReturnsSuccess()
    {
        var executor = ExecutorFactory.Create(/* no policies */);
        var result   = await executor.ExecuteAsync(new SampleContext());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.PoliciesEvaluated);
        Assert.Empty(result.AllViolations);
    }

    // ── MaxDegreeOfParallelism (Scenario 6) ────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MaxDegreeOfParallelism_CompletesWithCorrectResults()
    {
        // Tier 1: AlwaysPass + AlsoPass (same Order). Tier 2: AlwaysFail.
        var executor = ExecutorFactory.Create(
            new AlwaysPassPolicy(),
            new AlsoPassPolicy(),
            new AlwaysFailPolicy());

        var result = await executor.ExecuteAsync(new SampleContext(),
            new PolicyExecutionOptions
            {
                ParallelizeSameOrderTier = true,
                MaxDegreeOfParallelism  = 1  // Serialize within tier
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(3, result.PoliciesEvaluated);
        Assert.Single(result.BlockingViolations);
        Assert.Equal("T-001", result.BlockingViolations[0].Code);
    }

    // ── Combined options (Scenario 7) ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FailFastAndThrowOnFailure_ThrowsAfterFirstBlockingViolation()
    {
        var executor = ExecutorFactory.Create(
            new AlwaysPassPolicy(),
            new AlwaysFailPolicy(),
            new AlwaysWarnPolicy());

        var ex = await Assert.ThrowsAsync<PolicyViolationException>(() =>
            executor.ExecuteAsync(new SampleContext(), new PolicyExecutionOptions
            {
                Strategy       = ExecutionStrategy.FailFast,
                ThrowOnFailure = true
            }));

        Assert.NotNull(ex.AggregatedResult);
        Assert.True(ex.AggregatedResult.IsFailure);
        // FailFast: pipeline stopped at AlwaysFail; AlwaysWarn never ran
        Assert.Equal(2, ex.AggregatedResult.PoliciesEvaluated);
        Assert.DoesNotContain(ex.AggregatedResult.Results, r => r.PolicyName == "Test.AlwaysWarn");
    }

    [Fact]
    public async Task ExecuteAsync_BypassedPoliciesWithCollectAll_SkipsBypassedAndCollectsRest()
    {
        var executor = ExecutorFactory.Create(
            new AlwaysPassPolicy(),
            new AlwaysFailPolicy(),
            new AlwaysWarnPolicy());

        var result = await executor.ExecuteAsync(new SampleContext(), new PolicyExecutionOptions
        {
            BypassedPolicies = new HashSet<string> { "Test.AlwaysFail" },
            Strategy         = ExecutionStrategy.CollectAll
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.PoliciesEvaluated);
        Assert.Single(result.AdvisoryViolations);
        Assert.Empty(result.BlockingViolations);
    }
}
