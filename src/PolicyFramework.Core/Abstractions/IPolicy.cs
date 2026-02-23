namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Core policy contract. All ERP policies implement this interface.
/// Policies must be:
///   - Stateless        (no instance fields holding mutable state)
///   - Side-effect-free (read-only evaluation; mutations belong in domain services)
///   - Independently testable (no DI container required for unit tests)
///   - Automatically registered via assembly scanning
/// </summary>
/// <typeparam name="TContext">The bounded context this policy operates on.</typeparam>
public interface IPolicy<TContext> where TContext : IPolicyContext
{
    /// <summary>
    /// Human-readable policy identifier used in audit trails, logs, and violation reports.
    /// Convention: "Module.PolicyName" e.g. "Inventory.NegativeStock"
    /// </summary>
    string PolicyName { get; }

    /// <summary>
    /// Execution order within a pipeline. Lower values execute first.
    /// Policies sharing the same Order run in DI registration order
    /// (or concurrently when ParallelizeSameOrderTier is enabled).
    /// See PolicyOrderingConventions for recommended ranges.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Whether this policy participates in pipeline execution.
    /// Allows runtime toggling without removing from the DI container.
    /// Use cases: feature flags, tenant configuration, environment-specific rules.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Evaluates the policy against the supplied context.
    /// Must be side-effect free — pure read-only assessment.
    /// Any CancellationToken should be passed through to I/O operations.
    /// </summary>
    /// <param name="context">The immutable context carrying all evaluation data.</param>
    /// <param name="cancellationToken">Propagated from the application pipeline.</param>
    /// <returns>
    /// A <see cref="PolicyResult"/> describing the outcome.
    /// Returns Success when no violations are found.
    /// Returns Failure (with one or more violations) when the policy is violated.
    /// </returns>
    Task<PolicyResult> EvaluateAsync(TContext context, CancellationToken cancellationToken = default);
}
