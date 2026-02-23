namespace PolicyFramework.Host.Configuration;

/// <summary>
/// Configuration options for the Policy Framework host application.
/// Bound from the "PolicyFramework" section in appsettings.json.
/// </summary>
public sealed class PolicyFrameworkHostOptions
{
    /// <summary>
    /// Maximum number of days into the future that posting is allowed.
    /// Used by FutureDatePostingPolicy.
    /// </summary>
    public int FutureDatePostingMaxDays { get; init; } = 60;

    /// <summary>
    /// Credit limit utilisation threshold (0–1) at which a warning is emitted.
    /// Default 0.85 = 85%.
    /// </summary>
    public decimal CreditLimitWarningThreshold { get; init; } = 0.85m;

    /// <summary>
    /// Threshold below which an adjustment reason becomes mandatory.
    /// Negative values = stock reductions (e.g. -50 means reductions &gt; 50 require a reason).
    /// </summary>
    public decimal AdjustmentReasonMandatoryThreshold { get; init; } = -50m;
}
