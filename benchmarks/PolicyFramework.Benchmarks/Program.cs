using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace PolicyFramework.Benchmarks;

/// <summary>
/// Run: dotnet run -c Release --project benchmarks/PolicyFramework.Benchmarks
/// Filter: dotnet run -c Release --project benchmarks/PolicyFramework.Benchmarks -- --filter "*FivePolicies*"
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        BenchmarkRunner.Run<ExecutorComparisonBenchmarks>(config, args);
    }
}
