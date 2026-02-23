using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PolicyFramework.Host.Configuration;
using PolicyFramework.Host.Demo;

// =============================================================================
// Host configuration — DI registration
// =============================================================================

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(opts =>
        {
            opts.IncludeScopes   = false;
            opts.SingleLine     = true;
            opts.TimestampFormat = "HH:mm:ss.fff ";
        });
        // Use configuration for log level; Development environment gets Debug
        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
    })
    .ConfigureServices((context, services) =>
    {
        services.AddPolicyFrameworkWithConfiguration(context.Configuration);
    })
    .Build();

// =============================================================================
// Run demos
// =============================================================================

await DemoRunner.RunAsync(host.Services);
