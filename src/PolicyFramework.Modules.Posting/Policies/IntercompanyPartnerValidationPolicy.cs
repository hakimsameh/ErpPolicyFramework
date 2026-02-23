using PolicyFramework.Core.Abstractions;

namespace PolicyFramework.Modules.Posting.Policies;

/// <summary>
/// Validates intercompany transaction requirements.
///
/// Business rules:
///   1. Intercompany documents must specify a partner company code.
///   2. The partner code must differ from the posting company code
///      (a company cannot be its own intercompany partner).
///
/// Non-intercompany documents are skipped (early return Pass).
///
/// Order: 10 (Business Rule tier — only runs after hard gates pass)
/// </summary>
public sealed class IntercompanyPartnerValidationPolicy : PolicyBase<PostingContext>
{
    /// <summary>Policy name for bypass/config — use instead of hardcoded string.</summary>
    public const string Name = "Posting.IntercompanyPartnerValidation";
    public override string PolicyName => Name;
    public override int Order => PolicyOrderingConventions.BusinessRuleMin; // = 10

    public override Task<PolicyResult> EvaluateAsync(
        PostingContext context,
        CancellationToken cancellationToken = default)
    {
        // Policy only applies to intercompany documents
        if (!context.IsIntercompany)
            return Task.FromResult(Pass());

        // Rule 1: Partner code is required
        if (string.IsNullOrWhiteSpace(context.IntercompanyPartnerCode))
        {
            return Task.FromResult(Fail(
                code: PostingErrorCodes.IntercompanyPartnerMissing,
                message: $"Document '{context.DocumentNumber}' is flagged as intercompany " +
                         $"but no partner company code was provided. " +
                         $"Specify the intercompany partner before posting.",
                field: nameof(context.IntercompanyPartnerCode)));
        }

        // Rule 2: Partner must differ from posting company
        if (string.Equals(
            context.IntercompanyPartnerCode.Trim(),
            context.CompanyCode.Trim(),
            StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Fail(
                code: PostingErrorCodes.InvalidIntercompanyPartner,
                message: $"Intercompany partner code '{context.IntercompanyPartnerCode}' " +
                         $"cannot equal the posting company code '{context.CompanyCode}'. " +
                         $"A company cannot be its own intercompany partner.",
                field: nameof(context.IntercompanyPartnerCode)));
        }

        return Task.FromResult(Pass());
    }
}
