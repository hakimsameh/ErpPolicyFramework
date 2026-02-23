namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Severity classification for a policy violation.
/// Maps to ERP notification levels, workflow triggers, and pipeline blocking behavior.
/// </summary>
public enum PolicySeverity
{
    /// <summary>
    /// Informational signal. Business can proceed; used for downstream event triggers
    /// (e.g. raise a purchase order when stock hits reorder point).
    /// Does NOT block the pipeline.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Advisory condition. Business can proceed with awareness.
    /// May require an explicit acknowledgement in UI workflows.
    /// Does NOT block the pipeline.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Hard business rule violation. The transaction CANNOT proceed.
    /// BLOCKS the pipeline (IsSuccess = false on AggregatedPolicyResult).
    /// </summary>
    Error = 2,

    /// <summary>
    /// Systemic risk or infrastructure failure. Immediate escalation required.
    /// BLOCKS the pipeline. Triggers alerting in observability stack.
    /// </summary>
    Critical = 3
}
