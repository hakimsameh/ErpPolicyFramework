using PolicyFramework.Core.Abstractions;
using PolicyFramework.Modules.Posting;
using PolicyFramework.Modules.Posting.Policies;
using Xunit;

namespace PolicyFramework.Tests.Posting;

// =============================================================================
// Context builder
// =============================================================================

file static class ContextBuilder
{
    public static PostingContext Build(
        decimal          totalDebit     = 10_000m,
        decimal          totalCredit    = 10_000m,
        FiscalPeriodStatus periodStatus = FiscalPeriodStatus.Open,
        DateTimeOffset?  postingDate    = null,
        bool             isIntercompany = false,
        string?          partnerCode    = null,
        string           companyCode    = "CORP-01")
        => new()
        {
            DocumentNumber          = "TEST-DOC-001",
            DocumentType            = PostingDocumentType.JournalEntry,
            CompanyCode             = companyCode,
            LedgerCode              = "LEDGER-GEN",
            DocumentDate            = DateTimeOffset.UtcNow,
            PostingDate             = postingDate ?? DateTimeOffset.UtcNow,
            FiscalYear              = DateTimeOffset.UtcNow.Year,
            FiscalPeriod            = DateTimeOffset.UtcNow.Month,
            PeriodStatus = () => Task.FromResult(periodStatus),
            TotalDebit              = totalDebit,
            TotalCredit             = totalCredit,
            Currency                = "USD",
            PostedBy                = "tester",
            IsIntercompany          = isIntercompany,
            IntercompanyPartnerCode = partnerCode
        };
}

// =============================================================================
// BalancedEntryPolicy
// =============================================================================

public sealed class BalancedEntryPolicyTests
{
    private readonly BalancedEntryPolicy _sut = new();

    [Theory]
    [InlineData(1000,    1000)]     // exactly balanced
    [InlineData(1000,    999.99)]   // within tolerance (Δ = 0.01)
    [InlineData(5000.50, 5000.50)]  // matching decimal amounts
    public async Task EvaluateAsync_BalancedEntry_ReturnsSuccess(decimal debit, decimal credit)
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(debit, credit));
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(1000, 900)]     // Δ = 100
    [InlineData(5000, 4999)]    // Δ = 1 (> tolerance)
    [InlineData(0,    100)]     // all credit, no debit
    public async Task EvaluateAsync_ImbalancedEntry_ReturnsFailure(decimal debit, decimal credit)
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(debit, credit));
        Assert.True(result.IsFailure);
        Assert.Equal("PST-001", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_Violation_ContainsDebitCreditInMetadata()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(totalDebit: 1000, totalCredit: 900));
        var meta = result.Violations[0].Metadata!;
        Assert.True(meta.ContainsKey("TotalDebit"));
        Assert.True(meta.ContainsKey("TotalCredit"));
        Assert.Equal(1000m, (decimal)meta["TotalDebit"]);
        Assert.Equal(900m,  (decimal)meta["TotalCredit"]);
    }

    [Fact]
    public void Order_IsLowestInPipeline() =>
        Assert.Equal(PolicyOrderingConventions.HardGateMin, _sut.Order);
}

// =============================================================================
// OpenFiscalPeriodPolicy
// =============================================================================

public sealed class OpenFiscalPeriodPolicyTests
{
    private readonly OpenFiscalPeriodPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_OpenPeriod_Passes()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(periodStatus: FiscalPeriodStatus.Open));
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public async Task EvaluateAsync_ClosedPeriod_ReturnsError()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(periodStatus: FiscalPeriodStatus.Closed));
        Assert.True(result.IsFailure);
        Assert.Equal("PST-002", result.Violations[0].Code);
        Assert.Equal(PolicySeverity.Error, result.Violations[0].Severity);
    }

    [Fact]
    public async Task EvaluateAsync_LockedPeriod_ReturnsError()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(periodStatus: FiscalPeriodStatus.Locked));
        Assert.True(result.IsFailure);
        Assert.Equal("PST-002", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_ClosingPeriod_ReturnsWarning_AndStillPasses()
    {
        var result = await _sut.EvaluateAsync(ContextBuilder.Build(periodStatus: FiscalPeriodStatus.Closing));
        Assert.True(result.IsSuccess);         // warning: still passes
        Assert.Single(result.Violations);
        Assert.Equal("PST-W001", result.Violations[0].Code);
        Assert.Equal(PolicySeverity.Warning, result.Violations[0].Severity);
    }
}

// =============================================================================
// FutureDatePostingPolicy
// =============================================================================

public sealed class FutureDatePostingPolicyTests
{
    [Fact]
    public async Task EvaluateAsync_TodaysDate_Passes()
    {
        var sut    = new FutureDatePostingPolicy(maxFutureDays: 30);
        var result = await sut.EvaluateAsync(ContextBuilder.Build(postingDate: DateTimeOffset.UtcNow));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_WithinHorizon_Passes()
    {
        var sut    = new FutureDatePostingPolicy(maxFutureDays: 30);
        var result = await sut.EvaluateAsync(
            ContextBuilder.Build(postingDate: DateTimeOffset.UtcNow.AddDays(29)));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_BeyondHorizon_Fails()
    {
        var sut    = new FutureDatePostingPolicy(maxFutureDays: 30);
        var result = await sut.EvaluateAsync(
            ContextBuilder.Build(postingDate: DateTimeOffset.UtcNow.AddDays(31)));
        Assert.True(result.IsFailure);
        Assert.Equal("PST-005", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_PastDate_Passes()
    {
        var sut    = new FutureDatePostingPolicy(maxFutureDays: 30);
        var result = await sut.EvaluateAsync(
            ContextBuilder.Build(postingDate: DateTimeOffset.UtcNow.AddDays(-365)));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Constructor_NegativeDays_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new FutureDatePostingPolicy(-1));
}

// =============================================================================
// IntercompanyPartnerValidationPolicy
// =============================================================================

public sealed class IntercompanyPartnerValidationPolicyTests
{
    private readonly IntercompanyPartnerValidationPolicy _sut = new();

    [Fact]
    public async Task EvaluateAsync_NonIntercompanyDocument_Passes()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(isIntercompany: false));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_IntercompanyWithValidPartner_Passes()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(isIntercompany: true, partnerCode: "CORP-02", companyCode: "CORP-01"));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EvaluateAsync_IntercompanyWithNoPartnerCode_Fails()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(isIntercompany: true, partnerCode: null));
        Assert.True(result.IsFailure);
        Assert.Equal("PST-003", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_IntercompanyWithEmptyPartnerCode_Fails()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(isIntercompany: true, partnerCode: "   "));
        Assert.True(result.IsFailure);
        Assert.Equal("PST-003", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_IntercompanyWithSameCompanyCode_Fails()
    {
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(isIntercompany: true, partnerCode: "CORP-01", companyCode: "CORP-01"));
        Assert.True(result.IsFailure);
        Assert.Equal("PST-004", result.Violations[0].Code);
    }

    [Fact]
    public async Task EvaluateAsync_IntercompanyCaseInsensitiveMatch_Fails()
    {
        // "corp-01" == "CORP-01" → should fail (self-referencing intercompany)
        var result = await _sut.EvaluateAsync(
            ContextBuilder.Build(isIntercompany: true, partnerCode: "corp-01", companyCode: "CORP-01"));
        Assert.True(result.IsFailure);
        Assert.Equal("PST-004", result.Violations[0].Code);
    }
}
