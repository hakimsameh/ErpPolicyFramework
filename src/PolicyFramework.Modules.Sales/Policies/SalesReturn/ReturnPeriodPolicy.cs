using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Sales.Policies.SalesReturn;

/// <summary>
/// Ensures the return is within the allowed period from the original sale date.
/// </summary>
public sealed class ReturnPeriodPolicy : PolicyBase<SalesReturnContext>
{
    /// <summary>Policy name for bypass/config — use instead of hardcoded string.</summary>
    public const string Name = "Sales.Return.ReturnPeriod";
    public override string PolicyName => Name;
    public override int Order => PolicyOrderingConventions.HardGateMin + 2;

    public override Task<PolicyResult> EvaluateAsync(
        SalesReturnContext context,
        CancellationToken cancellationToken = default)
    {
        var daysSinceSale = (context.ReturnDate - context.OriginalSaleDate).TotalDays;

        if (daysSinceSale > context.MaxReturnPeriodDays)
        {
            return Task.FromResult(Fail(
                code: SalesErrorCodes.ReturnPeriodExceeded,
                message: $"Return period exceeded. Original sale: {context.OriginalSaleDate:yyyy-MM-dd}, " +
                         $"Return date: {context.ReturnDate:yyyy-MM-dd}. " +
                         $"Maximum allowed: {context.MaxReturnPeriodDays} days.",
                field: nameof(context.ReturnDate),
                metadata: new Dictionary<string, object>
                {
                    ["OriginalSaleDate"]    = context.OriginalSaleDate,
                    ["ReturnDate"]         = context.ReturnDate,
                    ["DaysSinceSale"]      = daysSinceSale,
                    ["MaxReturnPeriodDays"] = context.MaxReturnPeriodDays
                }));
        }

        return Task.FromResult(Pass());
    }
}
