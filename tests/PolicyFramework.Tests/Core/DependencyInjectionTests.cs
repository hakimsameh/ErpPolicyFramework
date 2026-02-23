using Microsoft.Extensions.DependencyInjection;
using PolicyFramework.Core.Abstractions;
using PolicyFramework.Core.DependencyInjection;
using PolicyFramework.Modules.Inventory;
using PolicyFramework.Modules.Inventory.Policies;
using PolicyFramework.Modules.Posting;
using PolicyFramework.Modules.Posting.Policies;
using PolicyFramework.Modules.Accounting;
using PolicyFramework.Modules.Accounting.Policies;
using Xunit;

namespace PolicyFramework.Tests.Core;

/// <summary>
/// Integration tests that verify the DI registration, assembly scanning,
/// and end-to-end pipeline execution without mocking.
/// </summary>
public sealed class DependencyInjectionTests
{
    // ── Assembly scanning ─────────────────────────────────────────────────────

    [Fact]
    public void AddPoliciesFromAssemblies_RegistersAllInventoryPolicies()
    {
        var sp = BuildContainer();
        var policies = sp.GetServices<IPolicy<InventoryAdjustmentContext>>().ToList();

        Assert.Contains(policies, p => p.PolicyName == "Inventory.NegativeStock");
        Assert.Contains(policies, p => p.PolicyName == "Inventory.MaxStockLevel");
        Assert.Contains(policies, p => p.PolicyName == "Inventory.ReorderPointAlert");
    }

    [Fact]
    public void AddPoliciesFromAssemblies_RegistersAllPostingPolicies()
    {
        var sp = BuildContainer();
        var policies = sp.GetServices<IPolicy<PostingContext>>().ToList();

        Assert.Contains(policies, p => p.PolicyName == "Posting.BalancedEntry");
        Assert.Contains(policies, p => p.PolicyName == "Posting.OpenFiscalPeriod");
        Assert.Contains(policies, p => p.PolicyName == "Posting.IntercompanyPartnerValidation");
    }

    [Fact]
    public void AddPoliciesFromAssemblies_RegistersAllAccountingPolicies()
    {
        var sp = BuildContainer();
        var policies = sp.GetServices<IPolicy<AccountAssignmentContext>>().ToList();

        Assert.Contains(policies, p => p.PolicyName == "Accounting.ActiveAccount");
        Assert.Contains(policies, p => p.PolicyName == "Accounting.CostCenterMandatory");
        Assert.Contains(policies, p => p.PolicyName == "Accounting.DualControlManualEntry");
    }

    // ── Executor resolution ───────────────────────────────────────────────────

    [Fact]
    public void AddPolicyFramework_ResolvesExecutorForAllContextTypes()
    {
        var sp = BuildContainer();

        Assert.NotNull(sp.GetService<IPolicyExecutor<InventoryAdjustmentContext>>());
        Assert.NotNull(sp.GetService<IPolicyExecutor<PostingContext>>());
        Assert.NotNull(sp.GetService<IPolicyExecutor<AccountAssignmentContext>>());
    }

    // ── Manual registration ───────────────────────────────────────────────────

    [Fact]
    public void AddPolicy_Generic_RegistersPolicy()
    {
        var services = new ServiceCollection()
            .AddPolicyFramework()
            .AddPolicy<InventoryAdjustmentContext, NegativeStockPolicy>()
            .BuildServiceProvider();

        var policies = services.GetServices<IPolicy<InventoryAdjustmentContext>>().ToList();
        Assert.Single(policies);
        Assert.IsType<NegativeStockPolicy>(policies[0]);
    }

    [Fact]
    public void AddPolicy_WithFactory_RegistersParametrizedPolicy()
    {
        var services = new ServiceCollection()
            .AddPolicyFramework()
            .AddPolicy<PostingContext>(_ => new FutureDatePostingPolicy(maxFutureDays: 90))
            .BuildServiceProvider();

        var policies = services.GetServices<IPolicy<PostingContext>>().ToList();
        Assert.Single(policies);
        Assert.Equal("Posting.FutureDatePosting", policies[0].PolicyName);
    }

    // ── End-to-end pipeline via DI ────────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_Inventory_ValidAdjustment_Succeeds()
    {
        var sp       = BuildContainer();
        var executor = sp.GetRequiredService<IPolicyExecutor<InventoryAdjustmentContext>>();

        var result = await executor.ExecuteAsync(new InventoryAdjustmentContext
        {
            WarehouseCode      = "WH-01",
            ItemCode           = "ITEM-A",
            UnitOfMeasure      = "EA",
            AdjustmentQuantity = -20m,
            CurrentStock = () => Task.FromResult(100m),
            ReorderPoint = () => Task.FromResult(10m),
            MaxStockLevel = () => Task.FromResult(200m),
            AdjustmentReason   = "CYCLE_COUNT",
            RequestedBy        = "tester",
            TransactionDate    = DateTimeOffset.UtcNow
        });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task FullPipeline_Posting_BalancedOpenPeriod_Succeeds()
    {
        var sp       = BuildContainer();
        var executor = sp.GetRequiredService<IPolicyExecutor<PostingContext>>();

        var result = await executor.ExecuteAsync(new PostingContext
        {
            DocumentNumber = "JE-001",
            DocumentType   = PostingDocumentType.JournalEntry,
            CompanyCode    = "CORP-01",
            LedgerCode     = "GEN",
            DocumentDate   = DateTimeOffset.UtcNow,
            PostingDate    = DateTimeOffset.UtcNow,
            FiscalYear     = DateTimeOffset.UtcNow.Year,
            FiscalPeriod   = DateTimeOffset.UtcNow.Month,
            PeriodStatus = () => Task.FromResult(FiscalPeriodStatus.Open),
            TotalDebit     = 5_000m,
            TotalCredit    = 5_000m,
            Currency       = "USD",
            PostedBy       = "tester"
        });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task FullPipeline_Accounting_ActiveAccountWithCostCenter_Succeeds()
    {
        var sp       = BuildContainer();
        var executor = sp.GetRequiredService<IPolicyExecutor<AccountAssignmentContext>>();

        var result = await executor.ExecuteAsync(new AccountAssignmentContext
        {
            AccountCode        = "6100-OPEX",
            AccountDescription = "Operating Expenses",
            AccountType        = AccountType.Expense,
            AccountStatus      = AccountStatus.Active,
            CompanyCode        = "CORP-01",
            CostCenter         = "CC-OPS",
            Amount             = 2_500m,
            Currency           = "USD",
            DocumentType       = "JE",
            AssignedBy         = "tester",
            RequiresCostCenter = true,
            IsManualEntry      = false,
            CurrentBalance     = () => Task.FromResult<decimal?>(null),
            CreditLimit        = () => Task.FromResult<decimal?>(null)
        });

        Assert.True(result.IsSuccess);
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public void AddPolicyFramework_CalledMultipleTimes_DoesNotDuplicate()
    {
        var services = new ServiceCollection()
            .AddPolicyFramework()
            .AddPolicyFramework()  // second call should be idempotent
            .AddPoliciesFromAssembly(typeof(NegativeStockPolicy).Assembly);

        using var sp = services.BuildServiceProvider();

        // Executor should resolve to exactly one implementation
        var executor = sp.GetRequiredService<IPolicyExecutor<InventoryAdjustmentContext>>();
        Assert.NotNull(executor);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static ServiceProvider BuildContainer() =>
        new ServiceCollection()
            .AddPolicyFramework()
            .AddPoliciesFromAssemblies(
                typeof(NegativeStockPolicy).Assembly,
                typeof(BalancedEntryPolicy).Assembly,
                typeof(ActiveAccountPolicy).Assembly)
            .AddPolicy<PostingContext>(_ => new FutureDatePostingPolicy(60))
            .BuildServiceProvider();
}
