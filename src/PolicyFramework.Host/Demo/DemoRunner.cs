using Microsoft.Extensions.DependencyInjection;
using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Host.Demo;

/// <summary>
/// Runs the predefined policy demo scenarios and formats output to the console.
/// </summary>
public sealed class DemoRunner
{
    private static readonly (string Title, Func<IServiceProvider, Task<AggregatedPolicyResult>> Run)[] Demos =
    [
        ("INVENTORY — Happy Path (all pass)", DemoScenarios.InventoryHappyPath),
        ("INVENTORY — Negative Stock + Missing Reason", DemoScenarios.InventoryMultipleViolations),
        ("INVENTORY — Reorder Point Alert (advisory only)", DemoScenarios.InventoryReorderAlert),
        ("POSTING  — Unbalanced Journal Entry", DemoScenarios.PostingUnbalanced),
        ("POSTING  — Locked Fiscal Period", DemoScenarios.PostingLockedPeriod),
        ("POSTING  — Closing Period (warning) + Intercompany", DemoScenarios.PostingClosingPeriodIntercompany),
        ("ACCOUNTING — Blocked Account", DemoScenarios.AccountingBlockedAccount),
        ("ACCOUNTING — Credit Limit Breach + Dual-Control", DemoScenarios.AccountingCreditBreach),
        ("STRATEGY — FailFast (stops at first blocking fault)", DemoScenarios.StrategyFailFast),
        ("STRATEGY — Parallel same-order tier", DemoScenarios.StrategyParallelTiers),
        ("RESILIENCE — Faulting policy handled gracefully", DemoScenarios.ResilienceFaultingPolicy),
    ];

    /// <summary>
    /// Executes all demo scenarios and writes formatted output to the console.
    /// </summary>
    public static async Task RunAsync(IServiceProvider serviceProvider)
    {
        PrintBanner();

        foreach (var (title, run) in Demos)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"┌─ {title}");
            Console.ResetColor();

            AggregatedPolicyResult result;
            try
            {
                result = await run(serviceProvider);
            }
            catch (PolicyViolationException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                var msg = ex.Message.Length > 120 ? ex.Message[..117] + "..." : ex.Message;
                Console.WriteLine($"│  ⚡ PolicyViolationException caught: {msg}");
                Console.ResetColor();
                result = ex.AggregatedResult;
            }

            PrintResult(result);
        }

        Console.WriteLine();
        PrintDivider('═');
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  All demos complete.");
        Console.ResetColor();
    }

    private static void PrintResult(AggregatedPolicyResult result)
    {
        var icon   = result.IsSuccess ? "✓" : "✗";
        var color  = result.IsSuccess ? ConsoleColor.Green : ConsoleColor.Red;
        var status = result.IsSuccess ? "PASS" : "FAIL";

        Console.ForegroundColor = color;
        Console.WriteLine($"│  {icon} {status} — Evaluated: {result.PoliciesEvaluated} " +
                          $"| Blocking: {result.BlockingViolations.Count} " +
                          $"| Advisory: {result.AdvisoryViolations.Count}");
        Console.ResetColor();

        foreach (var v in result.AllViolations)
        {
            var (vc, prefix) = v.Severity switch
            {
                PolicySeverity.Critical => (ConsoleColor.Magenta, "💥 CRITICAL"),
                PolicySeverity.Error => (ConsoleColor.Red, "  ✗ ERROR  "),
                PolicySeverity.Warning => (ConsoleColor.Yellow, "  ⚠ WARN   "),
                _ => (ConsoleColor.DarkGray, "  ℹ INFO   ")
            };

            Console.ForegroundColor = vc;
            Console.Write($"│     {prefix}  [{v.Code}]");
            Console.ResetColor();
            var msg = v.Message.Length > 100 ? v.Message[..97] + "..." : v.Message;
            Console.WriteLine($" {msg}");
            if (v.Field is not null)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"│              Field: {v.Field}");
                Console.ResetColor();
            }
        }

        if (result.IsSuccess && result.AllViolations.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("│     (no violations)");
            Console.ResetColor();
        }
    }

    private static void PrintBanner()
    {
        PrintDivider('═');
        Console.ForegroundColor = ConsoleColor.White;
        // POLICY
        Console.WriteLine("  ██████╗  ██████╗ ██╗     ██╗ ██████╗██╗   ██╗");
        Console.WriteLine("  ██╔══██╗██╔═══██╗██║     ██║██╔════╝╚██╗ ██╔╝");
        Console.WriteLine("  ██████╔╝██║   ██║██║     ██║██║      ╚████╔╝ ");
        Console.WriteLine("  ██╔═══╝ ██║   ██║██║     ██║██║       ╚██╔╝  ");
        Console.WriteLine("  ██║     ╚██████╔╝███████╗██║╚██████╗   ██║   ");
        Console.WriteLine("  ╚═╝      ╚═════╝ ╚══════╝╚═╝ ╚═════╝   ╚═╝   ");
        Console.WriteLine();
        // FRAMEWORK
        Console.WriteLine("  ███████╗██████╗  █████╗ ███╗   ███╗███████╗██╗    ██╗ ██████╗ ██████╗ ██╗  ██╗");
        Console.WriteLine("  ██╔════╝██╔══██╗██╔══██╗████╗ ████║██╔════╝██║    ██║██╔═══██╗██╔══██╗██║ ██╔╝");
        Console.WriteLine("  █████╗  ██████╔╝███████║██╔████╔██║█████╗  ██║ █╗ ██║██║   ██║██████╔╝█████╔╝ ");
        Console.WriteLine("  ██╔══╝  ██╔══██╗██╔══██║██║╚██╔╝██║██╔══╝  ██║███╗██║██║   ██║██╔══██╗██╔═██╗ ");
        Console.WriteLine("  ██║     ██║  ██║██║  ██║██║ ╚═╝ ██║███████╗╚███╔███╔╝╚██████╔╝██║  ██║██║  ██╗");
        Console.WriteLine("  ╚═╝     ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝     ╚═╝╚══════╝ ╚══╝╚══╝  ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("  Generic ERP Policy Framework  ·  .NET 8  ·  DDD  ·  Clean Architecture");
        Console.ResetColor();
        PrintDivider('═');
    }

    private static void PrintDivider(char ch = '─')
    {
        const int defaultWidth = 80;
        int width = defaultWidth;
        try
        {
            var windowWidth = Console.WindowWidth;
            if (windowWidth > 0)
                width = Math.Min(windowWidth - 1, defaultWidth);
        }
        catch
        {
            // Non-interactive context (pipe, CI, redirected output); use default
        }

        Console.WriteLine(new string(ch, width));
    }
}
