namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Immutable result value object returned by every <see cref="IPolicy{TContext}"/> evaluation.
/// Encapsulates the pass/fail decision and all associated violations for one policy.
///
/// Design notes:
///   - Warning violations set IsSuccess = true (warnings are advisory, not blocking)
///   - Error/Critical violations set IsSuccess = false
///   - Use factory methods; constructor is private
/// </summary>
public readonly record struct PolicyResult
{
    private readonly IReadOnlyList<PolicyViolation>? _violations;

    private PolicyResult(bool isSuccess, string policyName, IEnumerable<PolicyViolation> violations)
    {
        IsSuccess   = isSuccess;
        PolicyName  = policyName ?? string.Empty;
        _violations = [.. violations];
        EvaluatedAt = DateTimeOffset.UtcNow;
    }

    // -------------------------------------------------------------------------
    // Properties
    // -------------------------------------------------------------------------

    /// <summary>True when no blocking violations (Error/Critical) exist.</summary>
    public bool IsSuccess { get; }

    /// <summary>Inverse of IsSuccess for fluent readability.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The <see cref="IPolicy{TContext}.PolicyName"/> that produced this result.</summary>
    public string PolicyName { get; }

    /// <summary>All violations from this policy evaluation (may include warnings on a success result).</summary>
    public IReadOnlyList<PolicyViolation> Violations => _violations ?? [];

    /// <summary>UTC timestamp of evaluation. Used for audit trails.</summary>
    public DateTimeOffset EvaluatedAt { get; }

    /// <summary>True when at least one Error or Critical violation exists regardless of IsSuccess.</summary>
    public bool HasBlockingViolations =>
        Violations.Any(v => v.Severity >= PolicySeverity.Error);

    // -------------------------------------------------------------------------
    // Factory methods — the only way to construct a PolicyResult
    // -------------------------------------------------------------------------

    /// <summary>Creates a clean success result with no violations.</summary>
    public static PolicyResult Success(string policyName) =>
        new(true, policyName, []);

    /// <summary>Creates a failure result with a single violation.</summary>
    public static PolicyResult Failure(
        string policyName,
        string code,
        string message,
        PolicySeverity severity = PolicySeverity.Error,
        string? field = null,
        IDictionary<string, object>? metadata = null) =>
        new(false, policyName,
            [new PolicyViolation(code, message, severity, field, metadata)]);

    /// <summary>Creates a failure result with multiple violations.</summary>
    public static PolicyResult Failure(string policyName, IEnumerable<PolicyViolation> violations)
    {
        var list = violations.ToList();
        var hasBlocking = list.Any(v => v.Severity >= PolicySeverity.Error);
        return new(!hasBlocking, policyName, list); // isSuccess = false when blocking violations exist
    }

    /// <summary>
    /// Creates an advisory result with a Warning violation.
    /// IsSuccess remains <c>true</c> — warnings do not block the pipeline.
    /// </summary>
    public static PolicyResult Warning(
        string policyName,
        string code,
        string message,
        string? field = null) =>
        new(true, policyName,
            [new PolicyViolation(code, message, PolicySeverity.Warning, field)]);

    /// <summary>
    /// Creates an informational result.
    /// IsSuccess remains <c>true</c>.
    /// </summary>
    public static PolicyResult Info(
        string policyName,
        string code,
        string message,
        IDictionary<string, object>? metadata = null) =>
        new(true, policyName,
            [new PolicyViolation(code, message, PolicySeverity.Info, Metadata: metadata)]);
}
