namespace PolicyFramework.Modules.Accounting.Policies;

/// <summary>
/// Centralised error codes for Accounting policies.
/// </summary>
public static class AccountingErrorCodes
{
    public const string UnknownStatus = "ACC-000";
    public const string AccountBlocked = "ACC-001";
    public const string AccountInactive = "ACC-002";
    public const string CostCenterMissing = "ACC-003";
    public const string CreditLimitBreached = "ACC-004";
    
    public const string DualControlRequired = "ACC-W001";
    public const string CreditLimitWarning = "ACC-W002";
}
