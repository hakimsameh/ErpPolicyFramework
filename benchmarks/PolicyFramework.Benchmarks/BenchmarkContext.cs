using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Benchmarks;

/// <summary>
/// Minimal context for benchmarks — no I/O, no complex setup.
/// </summary>
public sealed class BenchmarkContext : IPolicyContext
{
    public int Value { get; init; }
}
