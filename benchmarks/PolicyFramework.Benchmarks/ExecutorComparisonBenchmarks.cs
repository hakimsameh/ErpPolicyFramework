using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PolicyFramework.Core.Abstractions;
using PolicyFramework.Core.DependencyInjection;
using PolicyFramework.Core.Execution;

namespace PolicyFramework.Benchmarks;

/// <summary>
/// Compares Generic IPolicyExecutor&lt;TContext&gt; vs Non-Generic IPolicyExecutor performance.
/// Measures the overhead of the PolicyExecutorDispatcher (service resolution + delegation).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class ExecutorComparisonBenchmarks
{
    private IPolicyExecutor<BenchmarkContext> _genericExecutor = null!;
    private IPolicyExecutor _nonGenericExecutor = null!;
    private BenchmarkContext _context = null!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection()
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Critical))
            .AddPolicyFramework()
            .AddTransient<IPolicy<BenchmarkContext>>(_ => new BenchmarkPassPolicy("Benchmark.P1", 1))
            .AddTransient<IPolicy<BenchmarkContext>>(_ => new BenchmarkPassPolicy("Benchmark.P2", 2))
            .AddTransient<IPolicy<BenchmarkContext>>(_ => new BenchmarkPassPolicy("Benchmark.P3", 3))
            .AddTransient<IPolicy<BenchmarkContext>>(_ => new BenchmarkPassPolicy("Benchmark.P4", 4))
            .AddTransient<IPolicy<BenchmarkContext>>(_ => new BenchmarkPassPolicy("Benchmark.P5", 5));

        var sp = services.BuildServiceProvider();

        _genericExecutor    = sp.GetRequiredService<IPolicyExecutor<BenchmarkContext>>();
        _nonGenericExecutor = sp.GetRequiredService<IPolicyExecutor>();
        _context            = new BenchmarkContext { Value = 42 };
    }

    /// <summary>Generic executor — direct call, no dispatcher.</summary>
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("FivePolicies")]
    public async Task<AggregatedPolicyResult> Generic_FivePolicies()
    {
        return await _genericExecutor.ExecuteAsync(_context);
    }

    /// <summary>Non-generic executor — dispatcher resolves generic per call.</summary>
    [Benchmark]
    [BenchmarkCategory("FivePolicies")]
    public async Task<AggregatedPolicyResult> NonGeneric_FivePolicies()
    {
        return await _nonGenericExecutor.ExecuteAsync(_context);
    }

    [Benchmark]
    [BenchmarkCategory("FivePolicies_WithOptions")]
    public async Task<AggregatedPolicyResult> Generic_FivePolicies_WithOptions()
    {
        return await _genericExecutor.ExecuteAsync(
            _context,
            new PolicyExecutionOptions { Strategy = ExecutionStrategy.CollectAll });
    }

    [Benchmark]
    [BenchmarkCategory("FivePolicies_WithOptions")]
    public async Task<AggregatedPolicyResult> NonGeneric_FivePolicies_WithOptions()
    {
        return await _nonGenericExecutor.ExecuteAsync(
            _context,
            new PolicyExecutionOptions { Strategy = ExecutionStrategy.CollectAll });
    }

}
