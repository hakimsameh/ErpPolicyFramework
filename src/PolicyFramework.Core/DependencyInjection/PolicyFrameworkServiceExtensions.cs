using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PolicyFramework.Core.Abstractions;
using PolicyFramework.Core.Execution;

namespace PolicyFramework.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering the Policy Framework in the DI container.
/// </summary>
public static class PolicyFrameworkServiceExtensions
{
    // =========================================================================
    // Framework bootstrap
    // =========================================================================

    /// <summary>
    /// Registers core Policy Framework infrastructure.
    ///
    /// This registers the open-generic <see cref="PolicyExecutor{TContext}"/> as the
    /// implementation of <see cref="IPolicyExecutor{TContext}"/> for ALL context types.
    /// DI resolves the correct closed generic on demand — no per-context registration needed.
    ///
    /// Call once in <c>Program.cs</c> / <c>Startup.ConfigureServices</c>.
    /// </summary>
    /// <example>
    /// builder.Services
    ///     .AddPolicyFramework()
    ///     .AddPoliciesFromAssemblies(typeof(InventoryModule).Assembly);
    /// </example>
    public static IServiceCollection AddPolicyFramework(this IServiceCollection services)
    {
        // Register necessary standard services required by PolicyExecutor
        services.AddLogging();
        services.AddOptions();

        // Open-generic registration: IPolicyExecutor<T> → PolicyExecutor<T>
        // TryAdd ensures this call is idempotent (safe to call from multiple modules).
        services.TryAddTransient(typeof(IPolicyExecutor<>), typeof(PolicyExecutor<>));
        return services;
    }

    // =========================================================================
    // Manual registration helpers
    // =========================================================================

    /// <summary>
    /// Registers a single policy implementation explicitly.
    ///
    /// Use when:
    ///   - The policy has constructor parameters that require a factory delegate
    ///   - You want to override a policy from a scanned assembly
    ///   - Auto-scanning is not desired for a specific policy
    /// </summary>
    /// <typeparam name="TContext">The policy context type.</typeparam>
    /// <typeparam name="TPolicy">The concrete policy type.</typeparam>
    /// <param name="services">Service Collection.</param>
    /// <param name="lifetime">Service lifetime. Transient is recommended (stateless policies).</param>
    public static IServiceCollection AddPolicy<TContext, TPolicy>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TContext : IPolicyContext
        where TPolicy  : class, IPolicy<TContext>
    {
        services.Add(new ServiceDescriptor(
            serviceType:            typeof(IPolicy<TContext>),
            implementationType:     typeof(TPolicy),
            lifetime:               lifetime));

        return services;
    }

    /// <summary>
    /// Registers a policy using a factory delegate.
    /// Required when the policy has constructor parameters that need custom wiring.
    /// </summary>
    public static IServiceCollection AddPolicy<TContext>(
        this IServiceCollection services,
        Func<IServiceProvider, IPolicy<TContext>> factory,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TContext : IPolicyContext
    {
        services.Add(new ServiceDescriptor(
            serviceType:  typeof(IPolicy<TContext>),
            factory:      factory,
            lifetime:     lifetime));

        return services;
    }

    // =========================================================================
    // Assembly scanning (auto-registration)
    // =========================================================================

    /// <summary>
    /// Scans the provided assemblies for all non-abstract <see cref="IPolicy{TContext}"/>
    /// implementations and registers them automatically.
    ///
    /// Includes both public and internal policy types. For internal policies to work,
    /// the module assembly must declare <c>InternalsVisibleTo</c> for the assembly
    /// that builds the DI container (e.g. your Host or startup project).
    /// </summary>
    /// <param name="services">Service Collection.</param>
    /// <param name="assemblies">One or more assemblies to scan.</param>
    /// 
    public static IServiceCollection AddPoliciesFromAssemblies(
        this IServiceCollection services,
        params Assembly[] assemblies)
        => services.AddPoliciesFromAssemblies(ServiceLifetime.Transient, assemblies);

    /// <summary>
    /// Scans the provided assemblies with a configurable service lifetime.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="lifetime">Service lifetime.</param>
    /// <param name="assemblies">Assemblies to scan.</param>
    public static IServiceCollection AddPoliciesFromAssemblies(
        this IServiceCollection services,
        ServiceLifetime lifetime,
        params Assembly[] assemblies)
        => services.AddPoliciesFromAssemblies(lifetime, excludeTypes: null, assemblies);

    /// <summary>
    /// Scans the provided assemblies, excluding specified policy types.
    /// Use when you need to register excluded types with custom configuration (e.g. factory).
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="lifetime">Service lifetime.</param>
    /// <param name="excludeTypes">Policy types to exclude from scanning (register separately).</param>
    /// <param name="assemblies">Assemblies to scan.</param>
    public static IServiceCollection AddPoliciesFromAssemblies(
        this IServiceCollection services,
        ServiceLifetime lifetime,
        Type[]? excludeTypes,
        params Assembly[] assemblies)
    {
        var excluded = excludeTypes is { Length: > 0 } ? new HashSet<Type>(excludeTypes) : null;
        var policyOpenGeneric = typeof(IPolicy<>);

        foreach (var assembly in assemblies)
        {
            var policyTypes = assembly
                .GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false, IsClass: true })
                .Where(t => ImplementsPolicyInterface(t, policyOpenGeneric))
                .Where(t => excluded is null || !excluded.Contains(t));

            foreach (var policyType in policyTypes)
            {
                // A policy may implement IPolicy<> for multiple contexts — register each
                var closedInterfaces = policyType
                    .GetInterfaces()
                    .Where(i => i.IsGenericType
                             && i.GetGenericTypeDefinition() == policyOpenGeneric);

                foreach (var closedInterface in closedInterfaces)
                {
                    services.Add(new ServiceDescriptor(
                        serviceType:        closedInterface,
                        implementationType: policyType,
                        lifetime:           lifetime));
                }
            }
        }

        return services;
    }

    /// <summary>
    /// Convenience overload: scans a single assembly (e.g. one ERP module).
    /// </summary>
    public static IServiceCollection AddPoliciesFromAssembly(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        => services.AddPoliciesFromAssemblies(lifetime, assembly);

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static bool ImplementsPolicyInterface(Type type, Type policyOpenGeneric) =>
        type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == policyOpenGeneric);
}
