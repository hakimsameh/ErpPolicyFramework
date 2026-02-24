namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Entry-point for executing all registered policies for a given context type.
/// Registered as an open-generic service — resolved automatically by DI for any TContext.
///
/// Consumers depend on this abstraction, never on the concrete executor.
/// </summary>
/// <typeparam name="TContext">The policy context type to evaluate.</typeparam>
public interface IPolicyExecutor<TContext> where TContext : IPolicyContext
{
    /// <summary>
    /// Discovers all <see cref="IPolicy{TContext}"/> implementations registered in DI,
    /// filters by <see cref="IPolicy{TContext}.IsEnabled"/>,
    /// sorts by <see cref="IPolicy{TContext}.Order"/> (ascending),
    /// and executes them according to <paramref name="options"/>.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="options">
    ///     Execution options. Pass null to use <see cref="PolicyExecutionOptions.Default"/>.
    /// </param>
    /// <param name="cancellationToken">Propagated to each policy evaluation.</param>
    /// <returns>
    ///     An <see cref="AggregatedPolicyResult"/> summarizing all evaluated policies.
    ///     Never throws (unless <see cref="PolicyExecutionOptions.ThrowOnFailure"/> is true).
    /// </returns>
    Task<AggregatedPolicyResult> ExecuteAsync(
        TContext context,
        PolicyExecutionOptions? options = null,
        CancellationToken cancellationToken = default);
}
/// <summary>
/// Non-generic policy executor allowing a single injection point for multiple context types.
/// Delegates to <see cref="IPolicyExecutor{TContext}"/> based on the context type at the call site.
/// Prefer injecting <see cref="IPolicyExecutor{TContext}"/> directly when only one context type is used.
/// </summary>
public interface IPolicyExecutor
{
    /// <summary>
    /// Executes all registered policies for the given context type.
    /// The context type is inferred from the argument — no reflection is used.
    /// </summary>
    /// <typeparam name="TContext">The policy context type.</typeparam>
    /// <param name="context">The evaluation context.</param>
    /// <param name="options">
    ///     Execution options. Pass null to use <see cref="PolicyExecutionOptions.Default"/>.
    /// </param>
    /// <param name="cancellationToken">Propagated to each policy evaluation.</param>
    /// <returns>
    ///     An <see cref="AggregatedPolicyResult"/> summarizing all evaluated policies.
    /// </returns>
    Task<AggregatedPolicyResult> ExecuteAsync<TContext>(
        TContext context,
        PolicyExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
        where TContext : IPolicyContext;
}
