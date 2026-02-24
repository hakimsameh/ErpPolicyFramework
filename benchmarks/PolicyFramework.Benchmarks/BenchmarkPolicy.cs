using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Benchmarks;

/// <summary>
/// Trivial policy that always passes — measures executor overhead, not policy logic.
/// </summary>
public sealed class BenchmarkPassPolicy : PolicyBase<BenchmarkContext>
{
    private readonly string _name;
    private readonly int _order;

    public BenchmarkPassPolicy(string name = "Benchmark.Pass1", int order = 1)
    {
        _name  = name;
        _order = order;
    }

    public override string PolicyName => _name;
    public override int Order => _order;

    public override Task<PolicyResult> EvaluateAsync(
        BenchmarkContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Pass());
}
