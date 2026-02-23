namespace PolicyFramework.Modules.Posting.Policies;

/// <summary>
/// Centralised error codes for Posting policies.
/// </summary>
public static class PostingErrorCodes
{
    public const string UnbalancedEntry = "PST-001";
    public const string FiscalPeriodClosedOrLocked = "PST-002";
    public const string IntercompanyPartnerMissing = "PST-003";
    public const string InvalidIntercompanyPartner = "PST-004";
    public const string FuturePostingHorizonExceeded = "PST-005";

    public const string FiscalPeriodClosing = "PST-W001";
}
