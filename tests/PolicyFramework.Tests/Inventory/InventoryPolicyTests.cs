using PolicyFramework.Core.Abstractions;
using PolicyFramework.Modules.Inventory;
using PolicyFramework.Modules.Inventory.Policies;
using Xunit;

namespace PolicyFramework.Tests.Inventory;

// =============================================================================
// Shared context builder
// =============================================================================

file static class ContextBuilder
{
    public static InventoryAdjustmentContext Build(
        decimal currentStock       = 500m,
        decimal adjustmentQuantity = -50m,
        decimal reorderPoint       = 100m,
        decimal maxStockLevel      = 1000m,
        string  adjustmentReason   = "CYCLE_COUNT",
        string  itemCode           = "ITEM-001",
        string  warehouseCode      = "WH-TEST",
        string  uom                = "EA")
        => new()
        {
            WarehouseCode      = warehouseCode,
            ItemCode           = itemCode,
            UnitOfMeasure      = uom,
            AdjustmentQuantity = adjustmentQuantity,
            CurrentStock = () => Task.FromResult(currentStock),
            ReorderPoint = () => Task.FromResult(reorderPoint),
            MaxStockLevel = () => Task.FromResult(maxStockLevel),
            AdjustmentReason   = adjustmentReason,
            RequestedBy        = "tester",
            TransactionDate    = DateTimeOffset.UtcNow
        };
}

// =============================================================================
// NegativeStockPolicy
// =============================================================================

public sealed class NegativeStockPolicyTests
{
    private readonly NegativeStockPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_WhenResultingStockIsPositive_ReturnsSuccess()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(currentStock: 100, adjustmentQuantity: -50));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task EvaluateAsync_WhenResultingStockIsExactlyZero_ReturnsSuccess()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(currentStock: 50, adjustmentQuantity: -50));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_WhenResultingStockIsNegative_ReturnsFailure()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(currentStock: 30, adjustmentQuantity: -100));
        Assert.True(result.IsFailure);
        Assert.Single(result.Violations);
        Assert.Equal("INV-001", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_Violation_HasErrorSeverity()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(currentStock: 10, adjustmentQuantity: -20));
        Assert.Equal(PolicySeverity.Error, result.Violations[0].Severity);
    }

    [Fact]
    public async Task EvaluateAsync_Violation_ContainsResultingStockInMetadata()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(currentStock: 10, adjustmentQuantity: -20));
        var metadata = result.Violations[0].Metadata!;
        Assert.True(metadata.ContainsKey("ResultingStock"));
        Assert.Equal(-10m, (decimal)metadata["ResultingStock"]);
    }

    [Fact]
    public async Task EvaluateAsync_Violation_HasCorrectFieldReference()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(currentStock: 5, adjustmentQuantity: -10));
        Assert.Equal(nameof(InventoryAdjustmentContext.AdjustmentQuantity), result.Violations[0].Field);
    }

    [Theory]
    [InlineData(0,     -1)]    // zero stock going negative
    [InlineData(100,   -101)]  // one over
    [InlineData(0.001,-0.002)] // fractional
    public async Task EvaluateAsync_VariousNegativeScenarios_AllFail(decimal current, decimal adjustment)
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(currentStock: current, adjustmentQuantity: adjustment));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void PolicyName_MatchesConvention() =>
        Assert.Equal("Inventory.NegativeStock", _sut.PolicyName);

    [Fact]
    public void Order_IsInBusinessRuleTier() =>
        Assert.InRange(_sut.Order, PolicyOrderingConventions.BusinessRuleMin, PolicyOrderingConventions.BusinessRuleMax);

    [Fact]
    public void IsEnabled_DefaultsToTrue() =>
        Assert.True(_sut.IsEnabled);
}

// =============================================================================
// AdjustmentReasonMandatoryPolicy
// =============================================================================

public sealed class AdjustmentReasonMandatoryPolicyTests
{
    private readonly AdjustmentReasonMandatoryPolicy _sut = new(threshold: -100m);

    [Fact]
    public async Task EvaluateAsync_SmallAdjustmentWithoutReason_Passes()
    {
        // Adjustment (-50) is above threshold (-100): reason not required
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(adjustmentQuantity: -50, adjustmentReason: ""));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_LargeAdjustmentWithReason_Passes()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(adjustmentQuantity: -150, adjustmentReason: "WRITE_OFF"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_LargeAdjustmentWithoutReason_Fails()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(adjustmentQuantity: -150, adjustmentReason: ""));
        Assert.True(result.IsFailure);
        Assert.Equal("INV-002", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_ExactlyAtThreshold_RequiresReason()
    {
        // Adjustment == threshold: reason IS required
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(adjustmentQuantity: -100, adjustmentReason: "  "));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task EvaluateAsync_WhitespaceOnlyReason_Fails()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(adjustmentQuantity: -200, adjustmentReason: "   "));
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Order_IsInHardGateTier() =>
        Assert.InRange(_sut.Order, PolicyOrderingConventions.HardGateMin, PolicyOrderingConventions.HardGateMax);
}

// =============================================================================
// MaxStockLevelPolicy
// =============================================================================

public sealed class MaxStockLevelPolicyTests
{
    private readonly MaxStockLevelPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_ResultingStockBelowMax_Passes()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(currentStock: 400, adjustmentQuantity: 100, maxStockLevel: 600));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task EvaluateAsync_ResultingStockExceedsMax_EmitsWarning()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(currentStock: 900, adjustmentQuantity: 200, maxStockLevel: 1000));
        Assert.True(result.IsSuccess);          // warnings don't fail
        Assert.Single(result.Violations);
        Assert.Equal("INV-W001", result.Violations[0].Code);
        Assert.Equal(PolicySeverity.Warning, result.Violations[0].Severity);
    }

    [Fact]
    public async Task EvaluateAsync_ZeroMaxStockLevel_IsIgnored()
    {
        // maxStockLevel = 0 means unconfigured — should always pass
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(currentStock: 999_999, adjustmentQuantity: 1, maxStockLevel: 0));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }
}

// =============================================================================
// ReorderPointAlertPolicy
// =============================================================================

public sealed class ReorderPointAlertPolicyTests
{
    private readonly ReorderPointAlertPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_StockStaysAboveReorderPoint_Passes()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(currentStock: 300, adjustmentQuantity: -50, reorderPoint: 100));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task EvaluateAsync_StockCrossesReorderPoint_EmitsWarning()
    {
        // Was 150 (above 100), will be 80 (below 100)
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(currentStock: 150, adjustmentQuantity: -70, reorderPoint: 100));
        Assert.True(result.IsSuccess);
        Assert.Single(result.Violations);
        Assert.Equal("INV-W002", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_AlreadyBelowReorderPoint_DoesNotRepeatAlert()
    {
        // Already below: 80 < 100; adjustment brings to 60
        // No threshold crossing occurred → no alert
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(currentStock: 80, adjustmentQuantity: -20, reorderPoint: 100));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task EvaluateAsync_ZeroReorderPoint_IsIgnored()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(currentStock: 10, adjustmentQuantity: -9, reorderPoint: 0));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }
}
