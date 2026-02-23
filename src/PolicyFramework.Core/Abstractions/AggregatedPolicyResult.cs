namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Aggregates results from every policy evaluated in a single pipeline run.
/// Provides a single pass/fail decision plus granular access to all outcomes.
///
/// Pipeline FAILS (IsSuccess = false) when any policy has a blocking violation (Error/Critical).
/// Warnings and Info violations do NOT cause failure.
/// </summary>
public sealed class AggregatedPolicyResult
{
    private readonly List<PolicyResult> _results;

    internal AggregatedPolicyResult(IEnumerable<PolicyResult> results, string contextTypeName)
    {
        _results        = [.. results];
        ContextTypeName = contextTypeName;
        ExecutedAt      = DateTimeOffset.UtcNow;
    }

    // -------------------------------------------------------------------------
    // Metadata
    // -------------------------------------------------------------------------

    /// <summary>The short name of the TContext type this pipeline ran against.</summary>
    public string ContextTypeName { get; }

    /// <summary>UTC timestamp of pipeline completion.</summary>
    public DateTimeOffset ExecutedAt { get; }

    // -------------------------------------------------------------------------
    // Individual results
    // -------------------------------------------------------------------------

    /// <summary>Ordered list of individual policy results (matches execution order).</summary>
    public IReadOnlyList<PolicyResult> Results => _results.AsReadOnly();

    /// <summary>Number of policies that were evaluated (disabled policies excluded).</summary>
    public int PoliciesEvaluated => _results.Count;

    /// <summary>Number of policies that produced at least one blocking violation.</summary>
    public int PoliciesFailed => _results.Count(r => r.HasBlockingViolations);

    // -------------------------------------------------------------------------
    // Aggregated violations
    // -------------------------------------------------------------------------

    /// <summary>All violations from all policies, flattened in execution order.</summary>
    public IReadOnlyList<PolicyViolation> AllViolations =>
        _results.SelectMany(r => r.Violations).ToList().AsReadOnly();

    /// <summary>Blocking violations only (Severity >= Error). These cause IsFailure = true.</summary>
    public IReadOnlyList<PolicyViolation> BlockingViolations =>
        AllViolations.Where(v => v.Severity >= PolicySeverity.Error).ToList().AsReadOnly();

    /// <summary>Non-blocking violations (Info + Warning). Pipeline still succeeds.</summary>
    public IReadOnlyList<PolicyViolation> AdvisoryViolations =>
        AllViolations.Where(v => v.Severity < PolicySeverity.Error).ToList().AsReadOnly();

    // -------------------------------------------------------------------------
    // Overall pass/fail
    // -------------------------------------------------------------------------

    /// <summary>
    /// True only when ZERO blocking (Error/Critical) violations exist across all evaluated policies.
    /// Advisory violations (Info/Warning) do not affect this value.
    /// </summary>
    public bool IsSuccess => !_results.Any(r => r.HasBlockingViolations);

    /// <summary>Inverse of IsSuccess for fluent readability.</summary>
    public bool IsFailure => !IsSuccess;

    // -------------------------------------------------------------------------
    // Convenience methods
    // -------------------------------------------------------------------------

    /// <summary>
    /// Throws <see cref="PolicyViolationException"/> if the pipeline has any blocking violations.
    /// Use when exception-propagation semantics are preferred over result inspection.
    /// </summary>
    /// <exception cref="PolicyViolationException">Thrown when IsFailure is true.</exception>
    public void ThrowIfFailed()
    {
        if (IsFailure)
            throw new PolicyViolationException(this);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"[PolicyPipeline:{ContextTypeName}] " +
        $"Success={IsSuccess} | " +
        $"Evaluated={PoliciesEvaluated} | " +
        $"Failed={PoliciesFailed} | " +
        $"Violations(all={AllViolations.Count}, " +
        $"blocking={BlockingViolations.Count}, " +
        $"advisory={AdvisoryViolations.Count})";
}

/// <summary>
/// Thrown by <see cref="AggregatedPolicyResult.ThrowIfFailed"/> when blocking violations exist.
/// Carries the full aggregated result for downstream inspection.
/// </summary>
public sealed class PolicyViolationException : Exception
{
    /// <summary>The aggregated pipeline result that triggered this exception.</summary>
    public AggregatedPolicyResult AggregatedResult { get; }

    public PolicyViolationException(AggregatedPolicyResult result)
        : base(BuildMessage(result))
    {
        AggregatedResult = result;
    }

    private static string BuildMessage(AggregatedPolicyResult result)
    {
        var violations = string.Join("; ",
            result.BlockingViolations.Select(v => $"[{v.Code}] {v.Message}"));

        return $"Policy pipeline '{result.ContextTypeName}' failed with " +
               $"{result.BlockingViolations.Count} blocking violation(s). " +
               $"Violations: {violations}";
    }
}
