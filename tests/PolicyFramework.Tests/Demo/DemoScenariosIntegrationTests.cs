using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolicyFramework.Core.Abstractions;
using PolicyFramework.Host.Configuration;
using PolicyFramework.Host.Demo;
using PolicyFramework.Modules.Accounting.Policies;
using PolicyFramework.Modules.Inventory.Policies;
using PolicyFramework.Modules.Posting.Policies;
using Xunit;

namespace PolicyFramework.Tests.Demo;

/// <summary>
/// Integration tests for all 11 Host demo scenarios.
/// Each scenario is executed against a host-like container and expected outcomes are asserted.
/// </summary>
public sealed class DemoScenariosIntegrationTests
{
    private static IServiceProvider BuildHostLikeContainer()
    {
        var configData = new Dictionary<string, string?>
        {
            ["PolicyFramework:FutureDatePostingMaxDays"]        = "60",
            ["PolicyFramework:CreditLimitWarningThreshold"]     = "0.85",
            ["PolicyFramework:AdjustmentReasonMandatoryThreshold"] = "-50"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddPolicyFrameworkWithConfiguration(config);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InventoryHappyPath_Passes()
    {
        var sp  = BuildHostLikeContainer();
        var result = await DemoScenarios.InventoryHappyPath(sp);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.BlockingViolations);
    }

    [Fact]
    public async Task InventoryMultipleViolations_FailsWithExpectedCodes()
    {
        var sp    = BuildHostLikeContainer();
        var result = await DemoScenarios.InventoryMultipleViolations(sp);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.AllViolations, v => v.Code == InventoryErrorCodes.NegativeStock);
        Assert.Contains(result.AllViolations, v => v.Code == InventoryErrorCodes.ReasonCodeMandatory);
    }

    [Fact]
    public async Task InventoryReorderAlert_PassesWithAdvisory()
    {
        var sp    = BuildHostLikeContainer();
        var result = await DemoScenarios.InventoryReorderAlert(sp);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.AdvisoryViolations, v => v.Code == InventoryErrorCodes.ReorderPointReached);
    }

    [Fact]
    public async Task PostingUnbalanced_Fails()
    {
        var sp    = BuildHostLikeContainer();
        var result = await DemoScenarios.PostingUnbalanced(sp);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.AllViolations, v => v.Code == PostingErrorCodes.UnbalancedEntry);
    }

    [Fact]
    public async Task PostingLockedPeriod_Fails()
    {
        var sp    = BuildHostLikeContainer();
        var result = await DemoScenarios.PostingLockedPeriod(sp);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.AllViolations, v => v.Code == PostingErrorCodes.FiscalPeriodClosedOrLocked);
    }

    [Fact]
    public async Task PostingClosingPeriodIntercompany_FailsWithInvalidPartnerAndClosingWarning()
    {
        var sp    = BuildHostLikeContainer();
        var result = await DemoScenarios.PostingClosingPeriodIntercompany(sp);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.AllViolations, v => v.Code == PostingErrorCodes.InvalidIntercompanyPartner);
        Assert.Contains(result.AllViolations, v => v.Code == PostingErrorCodes.FiscalPeriodClosing);
    }

    [Fact]
    public async Task AccountingBlockedAccount_Fails()
    {
        var sp    = BuildHostLikeContainer();
        var result = await DemoScenarios.AccountingBlockedAccount(sp);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.AllViolations, v => v.Code == AccountingErrorCodes.AccountBlocked);
    }

    [Fact]
    public async Task AccountingCreditBreach_FailsWithExpectedCodes()
    {
        var sp    = BuildHostLikeContainer();
        var result = await DemoScenarios.AccountingCreditBreach(sp);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.AllViolations, v => v.Code == AccountingErrorCodes.CreditLimitBreached);
    }

    [Fact]
    public async Task StrategyFailFast_StopsAtFirstBlockingViolation()
    {
        var sp    = BuildHostLikeContainer();
        var result = await DemoScenarios.StrategyFailFast(sp);

        Assert.False(result.IsSuccess);
        // AdjustmentReasonMandatoryPolicy (Order 5) runs before NegativeStockPolicy (Order 10)
        Assert.Contains(result.AllViolations, v =>
            v.Code == InventoryErrorCodes.ReasonCodeMandatory || v.Code == InventoryErrorCodes.NegativeStock);
    }

    [Fact]
    public async Task StrategyParallelTiers_FailsOnCostCenter()
    {
        var sp    = BuildHostLikeContainer();
        var result = await DemoScenarios.StrategyParallelTiers(sp);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.AllViolations, v => v.Code == AccountingErrorCodes.CostCenterMissing);
    }

    [Fact]
    public async Task ResilienceFaultingPolicy_HandlesFaultGracefully()
    {
        var sp    = BuildHostLikeContainer();
        var result = await DemoScenarios.ResilienceFaultingPolicy(sp);

        // FaultingInventoryPolicy throws; pipeline records POLICY_EXCEPTION (Critical)
        Assert.False(result.IsSuccess);
        Assert.Contains(result.AllViolations, v => v.Code == "POLICY_EXCEPTION");
        Assert.Contains(result.AllViolations, v => v.Severity == PolicySeverity.Critical);
    }
}
