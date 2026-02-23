namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Documents the recommended Order value ranges for ERP policies.
/// Using consistent ranges across all modules prevents ordering conflicts
/// and enables predictable cross-team composition.
/// </summary>
public static class PolicyOrderingConventions
{
    /// <summary>
    /// Hard-gate / prerequisite checks. Fast, I/O-free guards.
    /// Examples: null checks, enum validity, existence of referenced entities.
    /// Range: 1–9
    /// </summary>
    public const int HardGateMin  = 1;
    public const int HardGateMax  = 9;

    /// <summary>
    /// Core domain invariants and transactional business rule constraints.
    /// Examples: balanced entry, negative stock, credit limit.
    /// Range: 10–49
    /// </summary>
    public const int BusinessRuleMin = 10;
    public const int BusinessRuleMax = 49;

    /// <summary>
    /// Rules requiring data from multiple bounded contexts or external services.
    /// Examples: intercompany validation, cross-module budget checks.
    /// Range: 50–79
    /// </summary>
    public const int CrossModuleMin = 50;
    public const int CrossModuleMax = 79;

    /// <summary>
    /// Advisory signals and informational notifications.
    /// Examples: reorder point alerts, 90% credit utilization warning.
    /// Do not block the pipeline; used for downstream event handlers.
    /// Range: 80–99
    /// </summary>
    public const int AdvisoryMin = 80;
    public const int AdvisoryMax = 99;

    /// <summary>Default order assigned by <see cref="PolicyBase{TContext}"/>. Range: 100+</summary>
    public const int Default = 100;
}
