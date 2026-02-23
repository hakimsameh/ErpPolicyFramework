using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PolicyFramework.Core.DependencyInjection;
using PolicyFramework.Modules.Accounting;
using PolicyFramework.Modules.Accounting.Policies;
using PolicyFramework.Modules.Inventory;
using PolicyFramework.Modules.Inventory.Policies;
using PolicyFramework.Modules.Posting;
using PolicyFramework.Modules.Posting.Policies;

namespace PolicyFramework.Host.Configuration;

/// <summary>
/// Extension methods for registering the Policy Framework with configuration-driven options.
/// </summary>
public static class PolicyFrameworkHostServiceExtensions
{
    /// <summary>
    /// Registers the Policy Framework with all modules and configures parameterised policies
    /// from the "PolicyFramework" configuration section.
    /// </summary>
    public static IServiceCollection AddPolicyFrameworkWithConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<PolicyFrameworkHostOptions>(
            configuration.GetSection("PolicyFramework"));

        services.AddPolicyFramework();

        // Exclude parameterised policies; they are registered with config in AddParameterisedPolicies
        var excludeTypes = new[]
        {
            typeof(FutureDatePostingPolicy),
            typeof(CreditLimitPolicy),
            typeof(AdjustmentReasonMandatoryPolicy)
        };

        services.AddPoliciesFromAssemblies(
            ServiceLifetime.Transient,
            excludeTypes,
            typeof(NegativeStockPolicy).Assembly,
            typeof(BalancedEntryPolicy).Assembly,
            typeof(ActiveAccountPolicy).Assembly);

        services.AddParameterisedPolicies(configuration);

        return services;
    }

    /// <summary>
    /// Registers parameterised policies using values from configuration.
    /// Override with explicit registration when options are not available.
    /// </summary>
    public static IServiceCollection AddParameterisedPolicies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("PolicyFramework")
            .Get<PolicyFrameworkHostOptions>() ?? new PolicyFrameworkHostOptions();

        services.AddPolicy<PostingContext>(
            _ => new FutureDatePostingPolicy(maxFutureDays: options.FutureDatePostingMaxDays));

        services.AddPolicy<AccountAssignmentContext>(
            _ => new CreditLimitPolicy(warningThreshold: options.CreditLimitWarningThreshold));

        services.AddPolicy<InventoryAdjustmentContext>(
            _ => new AdjustmentReasonMandatoryPolicy(threshold: options.AdjustmentReasonMandatoryThreshold));

        return services;
    }
}
