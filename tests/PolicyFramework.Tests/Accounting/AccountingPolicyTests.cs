using PolicyFramework.Core.Abstractions;
using PolicyFramework.Modules.Accounting;
using PolicyFramework.Modules.Accounting.Policies;
using Xunit;

namespace PolicyFramework.Tests.Accounting;

// =============================================================================
// Context builder
// =============================================================================

file static class ContextBuilder
{
    public static AccountAssignmentContext Build(
        string        accountCode        = "4100-SALES",
        AccountType   accountType        = AccountType.Revenue,
        AccountStatus accountStatus      = AccountStatus.Active,
        string        costCenter         = "CC-TEST",
        decimal       amount             = 10_000m,
        bool          requiresCostCenter = false,
        bool          isManualEntry      = false,
        decimal?      creditLimit        = null,
        decimal?      currentBalance     = null)
        => new()
        {
            AccountCode        = accountCode,
            AccountDescription = $"Test account {accountCode}",
            AccountType        = accountType,
            AccountStatus      = accountStatus,
            CompanyCode        = "CORP-01",
            CostCenter         = costCenter,
            Amount             = amount,
            Currency           = "USD",
            DocumentType       = "JE",
            AssignedBy         = "tester",
            RequiresCostCenter = requiresCostCenter,
            IsManualEntry      = isManualEntry,
            CreditLimit = () => Task.FromResult(creditLimit),
            CurrentBalance = () => Task.FromResult(currentBalance
        )};
}

// =============================================================================
// ActiveAccountPolicy
// =============================================================================

public sealed class ActiveAccountPolicyTests
{
    private readonly ActiveAccountPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_ActiveAccount_Passes()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(accountStatus: AccountStatus.Active));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task EvaluateAsync_BlockedAccount_ReturnsError()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(accountStatus: AccountStatus.Blocked));
        Assert.True(result.IsFailure);
        Assert.Equal("ACC-001", result.Violations[0].Code);
        Assert.Equal(PolicySeverity.Error, result.Violations[0].Severity);
    }

    [Fact]
    public async Task EvaluateAsync_InactiveAccount_ReturnsError()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(accountStatus: AccountStatus.Inactive));
        Assert.True(result.IsFailure);
        Assert.Equal("ACC-002", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_Violation_HasAccountCodeFieldReference()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(accountStatus: AccountStatus.Blocked));
        Assert.Equal(nameof(AccountAssignmentContext.AccountCode), result.Violations[0].Field);
    }

    [Fact]
    public void Order_IsLowestPossible() =>
        Assert.Equal(PolicyOrderingConventions.HardGateMin, _sut.Order);
}

// =============================================================================
// CostCenterMandatoryPolicy
// =============================================================================

public sealed class CostCenterMandatoryPolicyTests
{
    private readonly CostCenterMandatoryPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_NotRequiredAndEmpty_Passes()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(costCenter: "", requiresCostCenter: false));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_RequiredAndProvided_Passes()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(costCenter: "CC-SALES", requiresCostCenter: true));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_RequiredAndEmpty_Fails()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(costCenter: "", requiresCostCenter: true));
        Assert.True(result.IsFailure);
        Assert.Equal("ACC-003", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_RequiredAndWhitespace_Fails()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(costCenter: "   ", requiresCostCenter: true));
        Assert.True(result.IsFailure);
    }
}

// =============================================================================
// CreditLimitPolicy
// =============================================================================

public sealed class CreditLimitPolicyTests
{
    private readonly CreditLimitPolicy _sut = new(warningThreshold: 0.90m);

    [Fact]
    public async Task EvaluateAsync_NoCreditLimitConfigured_Passes()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(amount: 500_000m, creditLimit: null, currentBalance: null));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_WellUnderLimit_Passes()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(amount: 10_000m, creditLimit: 100_000m, currentBalance: 50_000m));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task EvaluateAsync_Above90PercentUtilisation_EmitsWarning()
    {
        // 85k + 10k = 95k = 95% of 100k → warning
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(amount: 10_000m, creditLimit: 100_000m, currentBalance: 85_000m));
        Assert.True(result.IsSuccess);   // warning doesn't fail
        Assert.Single(result.Violations);
        Assert.Equal("ACC-W002", result.Violations[0].Code);
        Assert.Equal(PolicySeverity.Warning, result.Violations[0].Severity);
    }

    [Fact]
    public async Task EvaluateAsync_ExceedingLimit_ReturnsError()
    {
        // 95k + 80k = 175k > 100k → error
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(amount: 80_000m, creditLimit: 100_000m, currentBalance: 95_000m));
        Assert.True(result.IsFailure);
        Assert.Equal("ACC-004", result.Violations[0].Code);
        Assert.Equal(PolicySeverity.Error, result.Violations[0].Severity);
    }

    [Fact]
    public async Task EvaluateAsync_ExceedingLimit_MetadataContainsOverage()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(amount: 80_000m, creditLimit: 100_000m, currentBalance: 95_000m));
        var meta = result.Violations[0].Metadata!;
        Assert.True(meta.ContainsKey("Overage"));
        var overage = (decimal)meta["Overage"];
        Assert.Equal(75_000m, overage);  // 175k - 100k
    }

    [Fact]
    public async Task EvaluateAsync_ExactlyAtLimit_Passes()
    {
        // 50k + 50k = 100k = exactly at limit → pass (not over)
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(amount: 50_000m, creditLimit: 100_000m, currentBalance: 50_000m));
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(0.75, 80_000, 100_000, 10_000)] // 90% threshold, 90k/100k = pass
    public async Task EvaluateAsync_CustomThreshold_Respected(
        decimal threshold, decimal balance, decimal limit, decimal amount)
    {
        var sut    = new CreditLimitPolicy(warningThreshold: threshold);
        var result = await sut.EvaluateAsync(
            ContextBuilder.Build(amount: amount, creditLimit: limit, currentBalance: balance));
        // (80k + 10k) / 100k = 90% >= 75% threshold → warning
        Assert.Single(result.Violations);
        Assert.Equal("ACC-W002", result.Violations[0].Code);
    }
}

// =============================================================================
// DualControlManualEntryPolicy
// =============================================================================

public sealed class DualControlManualEntryPolicyTests
{
    private readonly DualControlManualEntryPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_NonManualEntry_Passes()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(accountType: AccountType.Revenue, isManualEntry: false));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }

    [Theory]
    [InlineData(AccountType.Expense)]
    [InlineData(AccountType.Asset)]
    public async Task EvaluateAsync_ManualEntryToNonSensitiveAccount_Passes(AccountType type)
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(accountType: type, isManualEntry: true));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }

    [Theory]
    [InlineData(AccountType.Revenue)]
    [InlineData(AccountType.Liability)]
    [InlineData(AccountType.Equity)]
    public async Task EvaluateAsync_ManualEntryToSensitiveAccount_EmitsWarning(AccountType type)
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(accountType: type, isManualEntry: true));
        Assert.True(result.IsSuccess);   // warning — pipeline continues
        Assert.Single(result.Violations);
        Assert.Equal("ACC-W001", result.Violations[0].Code);
        Assert.Equal(PolicySeverity.Warning, result.Violations[0].Severity);
    }
}

// =============================================================================
// Integration: DI-based pipeline test for accounting module
// =============================================================================

public sealed class AccountingPipelineIntegrationTests
{
    private static Microsoft.Extensions.DependencyInjection.ServiceProvider BuildContainer()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .AddPolicyFramework()
            .AddPoliciesFromAssembly(typeof(ActiveAccountPolicy).Assembly)
            .BuildServiceProvider();
        return services;
    }

    [Fact]
    public async Task Pipeline_BlockedAccount_CollectsAllViolations()
    {
        using var sp = BuildContainer();
        var executor = sp.GetRequiredService<IPolicyExecutor<AccountAssignmentContext>>();

        var ctx = ContextBuilder.Build(
            accountStatus:      AccountStatus.Blocked, // ACC-001
            costCenter:         "",
            requiresCostCenter: true,                  // ACC-003
            isManualEntry:      true,
            accountType:        AccountType.Revenue);  // ACC-W001

        var result = await executor.ExecuteAsync(ctx,
            new PolicyExecutionOptions { Strategy = ExecutionStrategy.CollectAll });

        Assert.True(result.IsFailure);
        Assert.Contains(result.BlockingViolations,  v => v.Code == "ACC-001");
        Assert.Contains(result.BlockingViolations,  v => v.Code == "ACC-003");
        // Note: ACC-001 (blocked account) might cause acc-003 to also fail
        // depending on whether active account blocks further evaluation.
    }

    [Fact]
    public async Task Pipeline_AllPoliciesPass_ReturnsSuccess()
    {
        using var sp = BuildContainer();
        var executor = sp.GetRequiredService<IPolicyExecutor<AccountAssignmentContext>>();

        var ctx = ContextBuilder.Build(
            accountStatus:      AccountStatus.Active,
            costCenter:         "CC-01",
            requiresCostCenter: true,
            isManualEntry:      false,
            creditLimit:        100_000m,
            currentBalance:     10_000m,
            amount:             5_000m);

        var result = await executor.ExecuteAsync(ctx);
        Assert.True(result.IsSuccess);
    }
}
