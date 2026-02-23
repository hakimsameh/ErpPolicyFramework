using PolicyFramework.Core.Abstractions;
using PolicyFramework.Modules.Inventory;

namespace PolicyFramework.Host.Demo;

/// <summary>
/// Deliberately faulting policy used to demonstrate pipeline resilience.
/// </summary>
internal sealed class FaultingInventoryPolicy : PolicyBase<InventoryAdjustmentContext>
{
    public override string PolicyName => "Demo.FaultingPolicy";
    public override int Order => 99;

    public override Task<PolicyResult> EvaluateAsync(
        InventoryAdjustmentContext context,
        CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "Simulated infrastructure failure (e.g. DB timeout, config error).");
}
