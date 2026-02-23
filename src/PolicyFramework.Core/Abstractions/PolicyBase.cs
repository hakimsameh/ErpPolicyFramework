namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Optional abstract base class for <see cref="IPolicy{TContext}"/> implementations.
/// Provides sensible defaults for <see cref="Order"/> and <see cref="IsEnabled"/>,
/// and exposes protected factory helpers that keep policy code concise.
///
/// Policies may implement <see cref="IPolicy{TContext}"/> directly if preferred.
/// </summary>
/// <typeparam name="TContext">The policy context type.</typeparam>
public abstract class PolicyBase<TContext> : IPolicy<TContext>
    where TContext : IPolicyContext
{
    // -------------------------------------------------------------------------
    // IPolicy<TContext> — abstract / virtual members
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public abstract string PolicyName { get; }

    /// <inheritdoc/>
    /// Defaults to 100. Override to control execution sequence within a pipeline.
    public virtual int Order => PolicyOrderingConventions.Default;

    /// <inheritdoc/>
    /// Defaults to true. Override or inject <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>
    /// / feature-flag service to provide dynamic toggling.
    public virtual bool IsEnabled => true;

    /// <inheritdoc/>
    public abstract Task<PolicyResult> EvaluateAsync(
        TContext context, CancellationToken cancellationToken = default);

    // -------------------------------------------------------------------------
    // Protected factory helpers — delegates to PolicyResult factories
    // Using these keeps subclass code clean: return Pass() / Fail(...) / Warn(...)
    // -------------------------------------------------------------------------

    /// <summary>Returns a success result with no violations.</summary>
    protected PolicyResult Pass() =>
        PolicyResult.Success(PolicyName);

    /// <summary>Returns a blocking failure result with a single violation.</summary>
    protected PolicyResult Fail(
        string code,
        string message,
        PolicySeverity severity = PolicySeverity.Error,
        string? field = null,
        IDictionary<string, object>? metadata = null) =>
        PolicyResult.Failure(PolicyName, code, message, severity, field, metadata);

    /// <summary>Returns a failure result with multiple violations.</summary>
    protected PolicyResult FailMany(IEnumerable<PolicyViolation> violations) =>
        PolicyResult.Failure(PolicyName, violations);

    /// <summary>
    /// Returns an advisory (warning) result. IsSuccess remains true.
    /// The pipeline continues; the warning is surfaced in AdvisoryViolations.
    /// </summary>
    protected PolicyResult Warn(string code, string message, string? field = null) =>
        PolicyResult.Warning(PolicyName, code, message, field);

    /// <summary>
    /// Returns an informational signal result. IsSuccess remains true.
    /// Use to trigger downstream event handlers without blocking the pipeline.
    /// </summary>
    protected PolicyResult Signal(
        string code,
        string message,
        IDictionary<string, object>? metadata = null) =>
        PolicyResult.Info(PolicyName, code, message, metadata);
}
