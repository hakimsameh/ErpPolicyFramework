namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Options governing a single pipeline execution.
/// Passed per-call so different call sites can use different strategies
/// against the same executor instance.
/// </summary>
public sealed class PolicyExecutionOptions
{
    /// <summary>Shared default instance — CollectAll, no exception, no parallelism.</summary>
    public static readonly PolicyExecutionOptions Default = new();

    /// <summary>
    /// Execution strategy. Default: <see cref="ExecutionStrategy.CollectAll"/>.
    /// </summary>
    public ExecutionStrategy Strategy { get; init; } = ExecutionStrategy.CollectAll;

    /// <summary>
    /// When true, <see cref="IPolicyExecutor{TContext}.ExecuteAsync"/> throws a
    /// <see cref="PolicyViolationException"/> if the pipeline fails.
    /// Default: false — callers inspect the returned <see cref="AggregatedPolicyResult"/>.
    /// </summary>
    public bool ThrowOnFailure { get; init; } = false;

    /// <summary>
    /// When true, all policies within the same <see cref="IPolicy{TContext}.Order"/> tier
    /// are executed concurrently via <c>Task.WhenAll</c>.
    /// Policies in different tiers remain sequential.
    /// Default: false — sequential execution within a tier.
    ///
    /// CAUTION: Enable only when policies in the same tier are truly independent
    /// and do not share writable resources.
    /// </summary>
    public bool ParallelizeSameOrderTier { get; init; } = false;

    /// <summary>
    /// Maximum degree of parallelism when ParallelizeSameOrderTier is true.
    /// Default: Environment.ProcessorCount.
    /// </summary>
    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;

    /// <summary>
    /// Names of policies that should be bypassed (skipped) during execution.
    /// </summary>
    public IReadOnlySet<string>? BypassedPolicies { get; init; }
}
