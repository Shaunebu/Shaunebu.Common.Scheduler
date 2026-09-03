using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shaunebu.Common.Scheduler.Abstractions;
using Shaunebu.Common.Scheduler.Client.Jobs;
using Shaunebu.Common.Scheduler.Extensions;
using Shaunebu.Common.Scheduler.Models;
using Shaunebu.Common.Scheduler.Persistence;

namespace Shaunebu.Common.Scheduler.Client.Infrastructure;

internal static class DemoSchedulerFactory
{
    public static ServiceProvider CreateProvider(
        Action<SchedulerOptions>? configureOptions = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(LogLevel.Critical);
        });

        services.AddShaunebuScheduler(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(2);
            options.JobExecutionTimeout = TimeSpan.FromSeconds(5);
            configureOptions?.Invoke(options);
        });
        services.AddSingleton<InMemoryJobHistoryRepository>();
        services.AddSingleton<IJobHistoryRepository>(provider => provider.GetRequiredService<InMemoryJobHistoryRepository>());

        services.AddSingleton<ScopedExecutionRecorder>();
        services.AddScoped<DemoScopedDependency>();
        services.AddTransient<FlakyScopedJob>();

        configureServices?.Invoke(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}
