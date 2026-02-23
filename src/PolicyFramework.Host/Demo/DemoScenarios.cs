using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using PolicyFramework.Core.Abstractions;
using PolicyFramework.Core.DependencyInjection;
using PolicyFramework.Modules.Accounting;
using PolicyFramework.Modules.Accounting.Policies;
using PolicyFramework.Modules.Inventory;
using PolicyFramework.Modules.Posting;

namespace PolicyFramework.Host.Demo;

/// <summary>
/// Predefined policy demo scenarios used by the console host.
/// </summary>
public static class DemoScenarios
{
    public static async Task<AggregatedPolicyResult> InventoryHappyPath(IServiceProvider sp)
    {
        var executor = sp.GetRequiredService<IPolicyExecutor<InventoryAdjustmentContext>>();
        return await executor.ExecuteAsync(new InventoryAdjustmentContext
        {
            WarehouseCode      = "WH-EAST",
            ItemCode           = "ITEM-1042",
            UnitOfMeasure      = "EA",
            AdjustmentQuantity = -50m,
            CurrentStock       = () => Task.FromResult(500m),
            ReorderPoint       = () => Task.FromResult(100m),
            MaxStockLevel      = () => Task.FromResult(1000m),
            AdjustmentReason   = "CYCLE_COUNT",
            RequestedBy        = "john.doe@corp.local",
            TransactionDate    = DateTimeOffset.UtcNow
        });
    }

    public static async Task<AggregatedPolicyResult> InventoryMultipleViolations(IServiceProvider sp)
    {
        var executor = sp.GetRequiredService<IPolicyExecutor<InventoryAdjustmentContext>>();
        return await executor.ExecuteAsync(new InventoryAdjustmentContext
        {
            WarehouseCode      = "WH-EAST",
            ItemCode           = "ITEM-1042",
            UnitOfMeasure      = "EA",
            AdjustmentQuantity = -600m,
            CurrentStock       = () => Task.FromResult(500m),
            ReorderPoint       = () => Task.FromResult(100m),
            MaxStockLevel      = () => Task.FromResult(1000m),
            AdjustmentReason   = "",
            RequestedBy        = "jane.doe@corp.local",
            TransactionDate    = DateTimeOffset.UtcNow
        });
    }

    public static async Task<AggregatedPolicyResult> InventoryReorderAlert(IServiceProvider sp)
    {
        var executor = sp.GetRequiredService<IPolicyExecutor<InventoryAdjustmentContext>>();
        return await executor.ExecuteAsync(new InventoryAdjustmentContext
        {
            WarehouseCode      = "WH-WEST",
            ItemCode           = "ITEM-2099",
            UnitOfMeasure      = "KG",
            AdjustmentQuantity = -30m,
            CurrentStock       = () => Task.FromResult(200m),
            ReorderPoint       = () => Task.FromResult(180m),
            MaxStockLevel      = () => Task.FromResult(500m),
            AdjustmentReason   = "PRODUCTION_CONSUMPTION",
            RequestedBy        = "ops@corp.local",
            TransactionDate    = DateTimeOffset.UtcNow
        });
    }

    public static async Task<AggregatedPolicyResult> PostingUnbalanced(IServiceProvider sp)
    {
        var executor = sp.GetRequiredService<IPolicyExecutor<PostingContext>>();
        return await executor.ExecuteAsync(new PostingContext
        {
            DocumentNumber = "JE-2024-00512",
            DocumentType   = PostingDocumentType.JournalEntry,
            CompanyCode    = "CORP-01",
            LedgerCode     = "LEDGER-GEN",
            DocumentDate   = DateTimeOffset.UtcNow,
            PostingDate    = DateTimeOffset.UtcNow,
            FiscalYear     = DateTimeOffset.UtcNow.Year,
            FiscalPeriod   = DateTimeOffset.UtcNow.Month,
            PeriodStatus   = () => Task.FromResult(FiscalPeriodStatus.Open),
            TotalDebit     = 10_000m,
            TotalCredit    = 9_500m,
            Currency       = "USD",
            PostedBy       = "accountant@corp.local"
        });
    }

    public static async Task<AggregatedPolicyResult> PostingLockedPeriod(IServiceProvider sp)
    {
        var executor = sp.GetRequiredService<IPolicyExecutor<PostingContext>>();
        var pastDate = DateTimeOffset.UtcNow.AddMonths(-3);
        return await executor.ExecuteAsync(new PostingContext
        {
            DocumentNumber = "INV-2024-00099",
            DocumentType   = PostingDocumentType.SalesInvoice,
            CompanyCode    = "CORP-01",
            LedgerCode     = "LEDGER-GEN",
            DocumentDate   = pastDate,
            PostingDate    = pastDate,
            FiscalYear     = pastDate.Year,
            FiscalPeriod   = pastDate.Month,
            PeriodStatus   = () => Task.FromResult(FiscalPeriodStatus.Locked),
            TotalDebit     = 5_000m,
            TotalCredit    = 5_000m,
            Currency       = "USD",
            PostedBy       = "accountant@corp.local"
        });
    }

    public static async Task<AggregatedPolicyResult> PostingClosingPeriodIntercompany(IServiceProvider sp)
    {
        var executor = sp.GetRequiredService<IPolicyExecutor<PostingContext>>();
        return await executor.ExecuteAsync(new PostingContext
        {
            DocumentNumber          = "JE-IC-2024-0033",
            DocumentType            = PostingDocumentType.JournalEntry,
            CompanyCode             = "CORP-01",
            LedgerCode              = "LEDGER-GEN",
            DocumentDate            = DateTimeOffset.UtcNow,
            PostingDate             = DateTimeOffset.UtcNow,
            FiscalYear              = DateTimeOffset.UtcNow.Year,
            FiscalPeriod            = DateTimeOffset.UtcNow.Month,
            PeriodStatus            = () => Task.FromResult(FiscalPeriodStatus.Closing),
            TotalDebit              = 25_000m,
            TotalCredit             = 25_000m,
            Currency                = "EUR",
            PostedBy                = "ic.accountant@corp.local",
            IsIntercompany          = true,
            IntercompanyPartnerCode = "CORP-01"
        });
    }

    public static async Task<AggregatedPolicyResult> AccountingBlockedAccount(IServiceProvider sp)
    {
        var executor = sp.GetRequiredService<IPolicyExecutor<AccountAssignmentContext>>();
        return await executor.ExecuteAsync(new AccountAssignmentContext
        {
            AccountCode        = "4100-SALES",
            AccountDescription = "Sales Revenue — Domestic",
            AccountType        = AccountType.Revenue,
            AccountStatus      = AccountStatus.Blocked,
            CompanyCode        = "CORP-01",
            CostCenter         = "CC-SALES",
            Amount             = 25_000m,
            Currency           = "USD",
            DocumentType       = "SI",
            AssignedBy         = "system",
            RequiresCostCenter = true,
            IsManualEntry      = false
        });
    }

    public static async Task<AggregatedPolicyResult> AccountingCreditBreach(IServiceProvider sp)
    {
        var executor = sp.GetRequiredService<IPolicyExecutor<AccountAssignmentContext>>();
        return await executor.ExecuteAsync(new AccountAssignmentContext
        {
            AccountCode        = "2100-AP",
            AccountDescription = "Accounts Payable",
            AccountType        = AccountType.Liability,
            AccountStatus      = AccountStatus.Active,
            CompanyCode        = "CORP-01",
            CostCenter         = "",
            Amount             = 80_000m,
            Currency           = "USD",
            DocumentType       = "PI",
            AssignedBy         = "finance.mgr@corp.local",
            RequiresCostCenter = false,
            IsManualEntry      = true,
            CreditLimit        = () => Task.FromResult<decimal?>(100_000m),
            CurrentBalance     = () => Task.FromResult<decimal?>(95_000m)
        });
    }

    public static async Task<AggregatedPolicyResult> StrategyFailFast(IServiceProvider sp)
    {
        var executor = sp.GetRequiredService<IPolicyExecutor<InventoryAdjustmentContext>>();
        return await executor.ExecuteAsync(
            new InventoryAdjustmentContext
            {
                WarehouseCode      = "WH-NORTH",
                ItemCode           = "ITEM-9999",
                UnitOfMeasure      = "EA",
                AdjustmentQuantity = -500m,
                CurrentStock       = () => Task.FromResult(100m),
                ReorderPoint       = () => Task.FromResult(50m),
                MaxStockLevel      = () => Task.FromResult(200m),
                AdjustmentReason   = "",
                RequestedBy        = "ops@corp.local",
                TransactionDate    = DateTimeOffset.UtcNow
            },
            new PolicyExecutionOptions
            {
                Strategy       = ExecutionStrategy.FailFast,
                ThrowOnFailure = false
            });
    }

    public static async Task<AggregatedPolicyResult> StrategyParallelTiers(IServiceProvider sp)
    {
        var executor = sp.GetRequiredService<IPolicyExecutor<AccountAssignmentContext>>();
        return await executor.ExecuteAsync(
            new AccountAssignmentContext
            {
                AccountCode        = "6100-OPEX",
                AccountDescription = "Operating Expenses",
                AccountType        = AccountType.Expense,
                AccountStatus      = AccountStatus.Active,
                CompanyCode        = "CORP-01",
                CostCenter         = "",
                Amount             = 5_000m,
                Currency           = "EUR",
                DocumentType       = "JE",
                AssignedBy         = "controller@corp.local",
                RequiresCostCenter = true,
                IsManualEntry      = false
            },
            new PolicyExecutionOptions
            {
                Strategy                 = ExecutionStrategy.CollectAll,
                ParallelizeSameOrderTier = true
            });
    }

    public static async Task<AggregatedPolicyResult> ResilienceFaultingPolicy(IServiceProvider sp)
    {
        var services = new ServiceCollection()
            .AddLogging(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning))
            .AddPolicyFramework()
            .AddTransient<IPolicy<InventoryAdjustmentContext>>(_ => new FaultingInventoryPolicy())
            .AddTransient<IPolicy<InventoryAdjustmentContext>>(_ =>
                new PolicyFramework.Modules.Inventory.Policies.NegativeStockPolicy())
            .BuildServiceProvider();

        var executor = services.GetRequiredService<IPolicyExecutor<InventoryAdjustmentContext>>();

        return await executor.ExecuteAsync(new InventoryAdjustmentContext
        {
            WarehouseCode      = "WH-DEMO",
            ItemCode           = "ITEM-FAULT",
            UnitOfMeasure      = "EA",
            AdjustmentQuantity = 10m,
            CurrentStock       = () => Task.FromResult(100m),
            ReorderPoint       = () => Task.FromResult(20m),
            MaxStockLevel      = () => Task.FromResult(500m),
            AdjustmentReason   = "TEST",
            RequestedBy        = "architect",
            TransactionDate    = DateTimeOffset.UtcNow
        });
    }
}
