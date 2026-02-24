using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Core.Execution;

/// <summary>
/// Default pipeline executor. Registered as an open-generic transient service.
///
/// Responsibilities:
///   1. Resolve all <see cref="IPolicy{TContext}"/> from DI (injected via IEnumerable)
///   2. Filter by <see cref="IPolicy{TContext}.IsEnabled"/>
///   3. Sort ascending by <see cref="IPolicy{TContext}.Order"/>
///   4. Execute sequentially or in parallel tiers per <see cref="PolicyExecutionOptions"/>
///   5. Handle faulting policies gracefully (Critical violation, never crash)
///   6. Emit structured logs at every stage for observability
/// </summary>
/// <typeparam name="TContext">The policy context type.</typeparam>
public sealed class PolicyExecutor<TContext> : IPolicyExecutor<TContext>
    where TContext : IPolicyContext
{
    private readonly ReadOnlyCollection<IPolicy<TContext>> _orderedPolicies;
    private readonly ILogger<PolicyExecutor<TContext>> _logger;

    /// <summary>
    /// Policies are sorted once at construction time.
    /// The executor is stateless after construction and safe for reuse.
    /// </summary>
    public PolicyExecutor(
        IEnumerable<IPolicy<TContext>> policies,
        ILogger<PolicyExecutor<TContext>> logger)
    {
        _orderedPolicies = policies
            .OrderBy(p => p.Order)
            .ToList()
            .AsReadOnly();

        _logger = logger;

        _logger.LogDebug(
            "PolicyExecutor<{Context}> initialized with {Count} registered policies: [{Names}]",
            typeof(TContext).Name,
            _orderedPolicies.Count,
            string.Join(", ", _orderedPolicies.Select(p => p.PolicyName)));
    }

    /// <inheritdoc/>
    public async Task<AggregatedPolicyResult> ExecuteAsync(
        TContext context,
        PolicyExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= PolicyExecutionOptions.Default;
        var contextName = typeof(TContext).Name;

        var bypassed = options.BypassedPolicies ?? new HashSet<string>();
        var enabledPolicies = _orderedPolicies
            .Where(p => p.IsEnabled && !bypassed.Contains(p.PolicyName))
            .ToList();

        _logger.LogInformation(
            "PolicyPipeline starting | Context={Context} | Strategy={Strategy} | " +
            "Registered={Registered} | Enabled={Enabled}",
            contextName, options.Strategy, _orderedPolicies.Count, enabledPolicies.Count);

        List<PolicyResult> results;

        if (options.ParallelizeSameOrderTier)
            results = await ExecuteInOrderTiersAsync(enabledPolicies, context, options, cancellationToken);
        else
            results = await ExecuteSequentiallyAsync(enabledPolicies, context, options, cancellationToken);

        var aggregated = new AggregatedPolicyResult(results, contextName);

        LogPipelineCompletion(aggregated);

        if (options.ThrowOnFailure)
            aggregated.ThrowIfFailed();

        return aggregated;
    }

    // =========================================================================
    // Private execution strategies
    // =========================================================================

    private async Task<List<PolicyResult>> ExecuteSequentiallyAsync(
        List<IPolicy<TContext>> policies,
        TContext context,
        PolicyExecutionOptions options,
        CancellationToken ct)
    {
        var results = new List<PolicyResult>(policies.Count);

        foreach (var policy in policies)
        {
            ct.ThrowIfCancellationRequested();

            var result = await ExecuteSinglePolicyAsync(policy, context, ct);
            results.Add(result);

            if (options.Strategy == ExecutionStrategy.FailFast
                && result.HasBlockingViolations)
            {
                _logger.LogInformation(
                    "PolicyPipeline FailFast triggered by '{Policy}' " +
                    "(Order={Order}). Stopping pipeline early.",
                    policy.PolicyName, policy.Order);
                break;
            }
        }

        return results;
    }

    private async Task<List<PolicyResult>> ExecuteInOrderTiersAsync(
        IList<IPolicy<TContext>> policies,
        TContext context,
        PolicyExecutionOptions options,
        CancellationToken ct)
    {
        var results = new List<PolicyResult>();

        // Group policies by Order value; execute each tier in parallel
        var tiers = policies
            .GroupBy(p => p.Order)
            .OrderBy(g => g.Key);

        foreach (var tier in tiers)
        {
            ct.ThrowIfCancellationRequested();

            var tierOrder = tier.Key;
            var tierPolicies = tier.ToList();

            _logger.LogDebug(
                "PolicyPipeline executing Order-tier {Order} with {Count} policies in parallel.",
                tierOrder, tierPolicies.Count);

            var tierResults = new PolicyResult[tierPolicies.Count];
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
                CancellationToken = ct
            };

            await Parallel.ForEachAsync(
                Enumerable.Range(0, tierPolicies.Count),
                parallelOptions,
                async (i, token) =>
                {
                    tierResults[i] = await ExecuteSinglePolicyAsync(tierPolicies[i], context, token);
                });

            results.AddRange(tierResults);

            if (options.Strategy == ExecutionStrategy.FailFast
                && tierResults.Any(r => r.HasBlockingViolations))
            {
                _logger.LogInformation(
                    "PolicyPipeline FailFast triggered in Order-tier {Order}. Stopping pipeline.",
                    tierOrder);
                break;
            }
        }

        return results;
    }

    // =========================================================================
    // Single policy execution with full exception guard
    // =========================================================================

    private async Task<PolicyResult> ExecuteSinglePolicyAsync(
        IPolicy<TContext> policy,
        TContext context,
        CancellationToken ct)
    {
        _logger.LogDebug(
            "Evaluating policy '{Policy}' (Order={Order})",
            policy.PolicyName, policy.Order);

        try
        {
            var result = await policy.EvaluateAsync(context, ct);

            if (result.IsFailure || result.HasBlockingViolations)
            {
                _logger.LogDebug(
                    "Policy '{Policy}' FAILED with {Count} violation(s): [{Codes}]",
                    policy.PolicyName,
                    result.Violations.Count,
                    string.Join(", ", result.Violations.Select(v => v.Code)));
            }
            else if (result.Violations.Count > 0)
            {
                _logger.LogDebug(
                    "Policy '{Policy}' passed with {Count} advisory violation(s): [{Codes}]",
                    policy.PolicyName,
                    result.Violations.Count,
                    string.Join(", ", result.Violations.Select(v => v.Code)));
            }
            else
            {
                _logger.LogDebug("Policy '{Policy}' passed cleanly.", policy.PolicyName);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // Do not swallow cancellation — re-propagate
            throw;
        }
        catch (Exception ex)
        {
            // A faulting policy must NEVER crash the pipeline.
            // Convert the exception to a Critical violation so the aggregate
            // correctly reflects failure and the caller gets a complete result.
            _logger.LogError(ex,
                "PolicyExecutor: policy '{Policy}' threw an unhandled {ExType}. " +
                "Converting to Critical violation to preserve pipeline integrity.",
                policy.PolicyName, ex.GetType().Name);

            return PolicyResult.Failure(
                policy.PolicyName,
                code: "POLICY_EXCEPTION",
                message: $"Policy '{policy.PolicyName}' encountered an unexpected error: {ex.Message}",
                severity: PolicySeverity.Critical,
                metadata: new Dictionary<string, object>
                {
                    ["ExceptionType"]    = ex.GetType().FullName ?? ex.GetType().Name,
                    ["ExceptionMessage"] = ex.Message,
                    ["PolicyOrder"]      = policy.Order
                });
        }
    }

    // =========================================================================
    // Logging helpers
    // =========================================================================

    private void LogPipelineCompletion(AggregatedPolicyResult aggregated)
    {
        _logger.LogInformation(
            "PolicyPipeline complete | {Summary}",
            aggregated.ToString());

        if (!aggregated.IsFailure) return;

        foreach (var violation in aggregated.BlockingViolations)
        {
            _logger.LogWarning(
                "PolicyViolation | Context={Context} | Code={Code} | " +
                "Severity={Severity} | Field={Field} | Message={Message}",
                aggregated.ContextTypeName,
                violation.Code,
                violation.Severity,
                violation.Field ?? "—",
                violation.Message);
        }
    }
}
