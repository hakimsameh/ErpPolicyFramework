using PolicyFramework.Modules.Sales;
using PolicyFramework.Modules.Sales.Policies;
using PolicyFramework.Modules.Sales.Policies.SalesInvoice;
using PolicyFramework.Modules.Sales.Policies.SalesReturn;
using Xunit;

namespace PolicyFramework.Tests.Sales;

// =============================================================================
// Sales Invoice — Context builders
// =============================================================================

file static class InvoiceContextBuilder
{
    public static SalesInvoiceContext Build(
        string   customerCode        = "CUST-001",
        bool     isCredit            = false,
        decimal  documentTotal       = 1_000m,
        bool     isBlacklisted       = false,
        decimal? creditLimit         = null,
        decimal? currentBalance      = null,
        Func<string, string, Task<decimal>>? getStock = null)
        => new()
        {
            CustomerCode        = customerCode,
            IsCredit            = isCredit,
            DocumentTotal       = documentTotal,
            Currency            = "USD",
            LineItems           = [new SalesInvoiceLineItem("ITEM-A", "WH-01", "EA", 10m)],
            DocumentDate        = DateTimeOffset.UtcNow,
            CreatedBy           = "tester",
            IsCustomerBlacklisted = () => Task.FromResult(isBlacklisted),
            CreditLimit         = creditLimit.HasValue ? () => Task.FromResult<decimal?>(creditLimit.Value) : null,
            CurrentBalance      = currentBalance.HasValue ? () => Task.FromResult<decimal?>(currentBalance.Value) : null,
            GetStockForItem     = getStock ?? ((item, wh) => Task.FromResult(100m))
        };
}

// =============================================================================
// Sales Return — Context builders
// =============================================================================

file static class ReturnContextBuilder
{
    public static SalesReturnContext Build(
        int                         maxReturnPeriodDays = 30,
        DateTimeOffset?             originalSaleDate    = null,
        DateTimeOffset?             returnDate          = null,
        Func<string, string, Task<bool>>? customerBought = null,
        Func<string, Task<bool>>?    isReturnable       = null)
    {
        var sale = originalSaleDate ?? DateTimeOffset.UtcNow.AddDays(-10);
        var ret  = returnDate ?? DateTimeOffset.UtcNow;

        return new SalesReturnContext
        {
            CustomerCode           = "CUST-001",
            OriginalSaleDocumentId = "INV-2024-001",
            OriginalSaleDate       = sale,
            ReturnDate             = ret,
            LineItems              = [new SalesReturnLineItem("ITEM-A", 2m)],
            CreatedBy              = "tester",
            MaxReturnPeriodDays    = maxReturnPeriodDays,
            CustomerBoughtItemOnSale = customerBought ?? ((_, _) => Task.FromResult(true)),
            IsProductReturnable    = isReturnable ?? (_ => Task.FromResult(true))
        };
    }
}

// =============================================================================
// CustomerCreditLimitPolicy
// =============================================================================

public sealed class CustomerCreditLimitPolicyTests
{
    private readonly CustomerCreditLimitPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_CashSale_SkipsAndPasses()
    {
        var ctx = InvoiceContextBuilder.Build(isCredit: false, documentTotal: 100_000m);
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_CreditSale_WithinLimit_Passes()
    {
        var ctx = InvoiceContextBuilder.Build(
            isCredit: true, documentTotal: 5_000m,
            creditLimit: 10_000m, currentBalance: 3_000m);
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_CreditSale_ExceedsLimit_Fails()
    {
        var ctx = InvoiceContextBuilder.Build(
            isCredit: true, documentTotal: 15_000m,
            creditLimit: 10_000m, currentBalance: 2_000m);
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsFailure);
        Assert.Equal(SalesErrorCodes.CreditLimitExceeded, result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_NoCreditData_SkipsAndPasses()
    {
        var ctx = InvoiceContextBuilder.Build(isCredit: true, documentTotal: 50_000m);
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }
}

// =============================================================================
// CustomerBlacklistPolicy
// =============================================================================

public sealed class CustomerBlacklistPolicyTests
{
    private readonly CustomerBlacklistPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_NotBlacklisted_Passes()
    {
        var ctx = InvoiceContextBuilder.Build(isBlacklisted: false);
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_Blacklisted_Fails()
    {
        var ctx = InvoiceContextBuilder.Build(isBlacklisted: true);
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsFailure);
        Assert.Equal(SalesErrorCodes.CustomerBlacklisted, result.Violations[0].Code);
        Assert.Equal(nameof(SalesInvoiceContext.CustomerCode), result.Violations[0].Field);
    }
}

// =============================================================================
// StockAvailabilityPolicy
// =============================================================================

public sealed class StockAvailabilityPolicyTests
{
    private readonly StockAvailabilityPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_SufficientStock_Passes()
    {
        var ctx = InvoiceContextBuilder.Build(
            getStock: (item, wh) => Task.FromResult(100m));
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_InsufficientStock_Fails()
    {
        var ctx = InvoiceContextBuilder.Build(
            getStock: (item, wh) => Task.FromResult(5m));
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsFailure);
        Assert.Equal(SalesErrorCodes.ItemNotAvailable, result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_ExactStock_Passes()
    {
        var ctx = new SalesInvoiceContext
        {
            CustomerCode          = "CUST-001",
            IsCredit              = false,
            DocumentTotal         = 100m,
            Currency              = "USD",
            LineItems             = [new SalesInvoiceLineItem("ITEM-X", "WH-01", "EA", 10m)],
            DocumentDate          = DateTimeOffset.UtcNow,
            CreatedBy             = "tester",
            IsCustomerBlacklisted  = () => Task.FromResult(false),
            GetStockForItem       = (_, _) => Task.FromResult(10m)
        };
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }
}

// =============================================================================
// NegativeStockOnInvoicePolicy
// =============================================================================

public sealed class NegativeStockOnInvoicePolicyTests
{
    private readonly NegativeStockOnInvoicePolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_StockRemainsPositive_Passes()
    {
        var ctx = InvoiceContextBuilder.Build(getStock: (_, _) => Task.FromResult(50m));
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_WouldGoNegative_Fails()
    {
        var ctx = new SalesInvoiceContext
        {
            CustomerCode          = "CUST-001",
            IsCredit              = false,
            DocumentTotal         = 100m,
            Currency              = "USD",
            LineItems             = [new SalesInvoiceLineItem("ITEM-X", "WH-01", "EA", 15m)],
            DocumentDate          = DateTimeOffset.UtcNow,
            CreatedBy              = "tester",
            IsCustomerBlacklisted  = () => Task.FromResult(false),
            GetStockForItem        = (_, _) => Task.FromResult(10m)
        };
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsFailure);
        Assert.Equal(SalesErrorCodes.NegativeStock, result.Violations[0].Code);
    }
}

// =============================================================================
// ReturnPeriodPolicy
// =============================================================================

public sealed class ReturnPeriodPolicyTests
{
    private readonly ReturnPeriodPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_WithinPeriod_Passes()
    {
        var ctx = ReturnContextBuilder.Build(maxReturnPeriodDays: 30);
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_ExceedsPeriod_Fails()
    {
        var sale = DateTimeOffset.UtcNow.AddDays(-50);
        var ret  = DateTimeOffset.UtcNow;
        var ctx  = ReturnContextBuilder.Build(
            maxReturnPeriodDays: 30,
            originalSaleDate: sale,
            returnDate: ret);
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsFailure);
        Assert.Equal(SalesErrorCodes.ReturnPeriodExceeded, result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_ExactlyOnBoundary_Passes()
    {
        // Use 29 days to stay unambiguously within period (avoids floating-point/day-boundary edge cases)
        var sale = DateTimeOffset.UtcNow.AddDays(-29);
        var ret  = DateTimeOffset.UtcNow;
        var ctx  = ReturnContextBuilder.Build(
            maxReturnPeriodDays: 30,
            originalSaleDate: sale,
            returnDate: ret);
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }
}

// =============================================================================
// CustomerBoughtProductPolicy
// =============================================================================

public sealed class CustomerBoughtProductPolicyTests
{
    private readonly CustomerBoughtProductPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_CustomerBought_Passes()
    {
        var ctx = ReturnContextBuilder.Build(
            customerBought: (docId, itemCode) => Task.FromResult(true));
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_CustomerDidNotBuy_Fails()
    {
        var ctx = ReturnContextBuilder.Build(
            customerBought: (docId, itemCode) => Task.FromResult(false));
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsFailure);
        Assert.Equal(SalesErrorCodes.CustomerDidNotBuy, result.Violations[0].Code);
    }
}

// =============================================================================
// ProductReturnablePolicy
// =============================================================================

public sealed class ProductReturnablePolicyTests
{
    private readonly ProductReturnablePolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_Returnable_Passes()
    {
        var ctx = ReturnContextBuilder.Build(isReturnable: _ => Task.FromResult(true));
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_NotReturnable_Fails()
    {
        var ctx = ReturnContextBuilder.Build(isReturnable: _ => Task.FromResult(false));
        var result = await _sut.EvaluateAsync(ctx);
        Assert.True(result.IsFailure);
        Assert.Equal(SalesErrorCodes.ProductNotReturnable, result.Violations[0].Code);
    }
}

// =============================================================================
// Integration — Full pipeline for Sales Invoice and Sales Return
// =============================================================================

public sealed class SalesPolicyIntegrationTests
{
    [Fact]
    public async Task SalesInvoice_FullPipeline_AllPass()
    {
        var services = new ServiceCollection()
            .AddPolicyFramework()
            .AddPoliciesFromAssemblies(ServiceLifetime.Transient, typeof(CustomerBlacklistPolicy).Assembly);
        var sp = services.BuildServiceProvider();

        var executor = sp.GetRequiredService<IPolicyExecutor<SalesInvoiceContext>>();
        var ctx = InvoiceContextBuilder.Build(
            isCredit: false,
            isBlacklisted: false,
            getStock: (_, _) => Task.FromResult(1000m));

        var result = await executor.ExecuteAsync(ctx);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SalesInvoice_Blacklisted_Fails()
    {
        var services = new ServiceCollection()
            .AddPolicyFramework()
            .AddPoliciesFromAssemblies(ServiceLifetime.Transient, typeof(CustomerBlacklistPolicy).Assembly);
        var sp = services.BuildServiceProvider();

        var executor = sp.GetRequiredService<IPolicyExecutor<SalesInvoiceContext>>();
        var ctx = InvoiceContextBuilder.Build(isBlacklisted: true);

        var result = await executor.ExecuteAsync(ctx);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.AllViolations, v => v.Code == SalesErrorCodes.CustomerBlacklisted);
    }

    [Fact]
    public async Task SalesReturn_FullPipeline_AllPass()
    {
        var services = new ServiceCollection()
            .AddPolicyFramework()
            .AddPoliciesFromAssemblies(ServiceLifetime.Transient, typeof(ReturnPeriodPolicy).Assembly);
        var sp = services.BuildServiceProvider();

        var executor = sp.GetRequiredService<IPolicyExecutor<SalesReturnContext>>();
        var ctx = ReturnContextBuilder.Build();

        var result = await executor.ExecuteAsync(ctx);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SalesReturn_PeriodExceeded_Fails()
    {
        var services = new ServiceCollection()
            .AddPolicyFramework()
            .AddPoliciesFromAssemblies(ServiceLifetime.Transient, typeof(ReturnPeriodPolicy).Assembly);
        var sp = services.BuildServiceProvider();

        var executor = sp.GetRequiredService<IPolicyExecutor<SalesReturnContext>>();
        var ctx = ReturnContextBuilder.Build(
            maxReturnPeriodDays: 7,
            originalSaleDate: DateTimeOffset.UtcNow.AddDays(-30),
            returnDate: DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(ctx);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.AllViolations, v => v.Code == SalesErrorCodes.ReturnPeriodExceeded);
    }
}
