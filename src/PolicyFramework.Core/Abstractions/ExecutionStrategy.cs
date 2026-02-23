namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Controls what the pipeline does after a blocking violation is encountered.
/// </summary>
public enum ExecutionStrategy
{
    /// <summary>
    /// Run ALL policies regardless of earlier failures.
    /// Produces the most complete validation report — ideal for UI form validation
    /// where showing all errors at once improves user experience.
    /// </summary>
    CollectAll,

    /// <summary>
    /// Stop the pipeline immediately on the first blocking (Error/Critical) violation.
    /// Ideal for performance-critical or side-effect-prone pipelines where
    /// evaluating later policies after an early hard failure is wasteful.
    /// </summary>
    FailFast
}
