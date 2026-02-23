namespace PolicyFramework.Modules.Sales.Policies;

/// <summary>Machine-readable error codes for Sales module policies.</summary>
public static class SalesErrorCodes
{
    // Sales Invoice
    public const string CreditLimitExceeded   = "SAL-001";
    public const string CustomerBlacklisted   = "SAL-002";
    public const string ItemNotAvailable      = "SAL-003";
    public const string NegativeStock         = "SAL-004";

    // Sales Return
    public const string CustomerDidNotBuy     = "SAL-101";
    public const string ProductNotReturnable  = "SAL-102";
    public const string ReturnPeriodExceeded  = "SAL-103";
}
