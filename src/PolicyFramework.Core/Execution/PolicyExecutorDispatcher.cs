using Microsoft.Extensions.DependencyInjection;
using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Core.Execution;

/// <summary>
/// Dispatches non-generic <see cref="IPolicyExecutor"/> calls to the appropriate
/// <see cref="IPolicyExecutor{TContext}"/> based on the context type at the call site.
/// </summary>
internal sealed class PolicyExecutorDispatcher(IServiceProvider serviceProvider) : IPolicyExecutor
{
    /// <inheritdoc/>
    public Task<AggregatedPolicyResult> ExecuteAsync<TContext>(
        TContext context,
        PolicyExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
        where TContext : IPolicyContext
    {
        ArgumentNullException.ThrowIfNull(context);
        var executor = serviceProvider.GetRequiredService<IPolicyExecutor<TContext>>();
        return executor.ExecuteAsync(context, options, cancellationToken);
    }
}
