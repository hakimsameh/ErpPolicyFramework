using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Posting.Policies;

/// <summary>
/// Prevents postings with a date beyond a configurable future horizon.
/// Configurable horizon supports different policies per company code or document type.
///
/// Business rule: Far-future postings are typically data-entry errors.
/// A 60-day horizon is a common ERP default.
///
/// Constructor parameter: <paramref name="maxFutureDays"/> allows configuration injection.
/// Register via factory delegate in DI when using non-default value.
///
/// Order: 5 (Hard Gate tier)
/// </summary>
public sealed class FutureDatePostingPolicy : PolicyBase<PostingContext>
{
    private readonly int _maxFutureDays;

    /// <summary>
    /// Initialises with a configurable future-date horizon.
    /// </summary>
    /// <param name="maxFutureDays">
    ///     Maximum number of days into the future a posting date may fall.
    ///     Default: 60.
    /// </param>
    public FutureDatePostingPolicy(int maxFutureDays = 60)
    {
        if (maxFutureDays < 0)
            throw new ArgumentOutOfRangeException(nameof(maxFutureDays),
                "maxFutureDays must be non-negative.");

        _maxFutureDays = maxFutureDays;
    }

    public override string PolicyName => "Posting.FutureDatePosting";
    public override int Order => PolicyOrderingConventions.HardGateMin + 4; // = 5

    public override Task<PolicyResult> EvaluateAsync(
        PostingContext context,
        CancellationToken cancellationToken = default)
    {
        var maxAllowedDate = DateTimeOffset.UtcNow.AddDays(_maxFutureDays).Date;
        var postingDay = context.PostingDate.UtcDateTime.Date;

        if (postingDay > maxAllowedDate)
        {
            return Task.FromResult(Fail(
                code: PostingErrorCodes.FuturePostingHorizonExceeded,
                message: $"Posting date {context.PostingDate:yyyy-MM-dd} exceeds the " +
                         $"maximum allowed future posting horizon of {_maxFutureDays} days " +
                         $"(max allowed: {maxAllowedDate:yyyy-MM-dd}). " +
                         $"Correct the posting date or contact your ERP administrator.",
                field: nameof(context.PostingDate),
                metadata: new Dictionary<string, object>
                {
                    ["PostingDate"] = context.PostingDate,
                    ["MaxAllowedDate"] = maxAllowedDate,
                    ["MaxFutureDays"] = _maxFutureDays
                }));
        }

        return Task.FromResult(Pass());
    }
}
