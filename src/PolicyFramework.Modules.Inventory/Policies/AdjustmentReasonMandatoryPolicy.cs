using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Inventory.Policies;

/// <summary>
/// Enforces that large negative stock adjustments carry a non-empty reason code.
///
/// Business rule: Adjustments that reduce stock by more than
/// <see cref="LargeAdjustmentThreshold"/> units must have a reason code
/// for SOX audit-trail compliance.
///
/// Order: 5 (Hard Gate tier — fast, I/O-free, runs before quantity checks)
/// </summary>
public sealed class AdjustmentReasonMandatoryPolicy : PolicyBase<InventoryAdjustmentContext>
{
    /// <summary>Threshold below which a reason code is mandatory. Default: -100.</summary>
    private readonly decimal _threshold;

    public AdjustmentReasonMandatoryPolicy(decimal threshold = -100m)
    {
        _threshold = threshold;
    }

    /// <summary>Policy name for bypass/config — use instead of hardcoded string.</summary>
    public const string Name = "Inventory.AdjustmentReasonMandatory";
    public override string PolicyName => Name;
    public override int Order => PolicyOrderingConventions.HardGateMin + 4; // = 5

    public override Task<PolicyResult> EvaluateAsync(
        InventoryAdjustmentContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.AdjustmentQuantity <= _threshold
            && string.IsNullOrWhiteSpace(context.AdjustmentReason))
        {
            return Task.FromResult(Fail(
                code: InventoryErrorCodes.ReasonCodeMandatory,
                message: $"A reason code is required for adjustments of {_threshold:N2} " +
                         $"{context.UnitOfMeasure} or less. " +
                         $"Requested adjustment: {context.AdjustmentQuantity:N2} {context.UnitOfMeasure}.",
                field: nameof(context.AdjustmentReason),
                metadata: new Dictionary<string, object>
                {
                    ["Threshold"] = _threshold,
                    ["AdjustmentQuantity"] = context.AdjustmentQuantity
                }));
        }

        return Task.FromResult(Pass());
    }
}
