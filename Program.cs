using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shaunebu.Common.Scheduler.Abstractions;
using Shaunebu.Common.Scheduler.Enums;
using Shaunebu.Common.Scheduler.Extensions;
using Shaunebu.Common.Scheduler.Models;
using Shaunebu.Common.Scheduler.Plugins;
using Shaunebu.Common.Scheduler.Providers;
using Shaunebu.Common.Scheduler.Triggers;



// Enhanced demo with all new features
var hostBuilder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add scheduler with enhanced features
        services.AddShaunebuScheduler(options =>
        {
            options.MaxConcurrentJobs = 3;
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
            options.ContinueOnFailure = true;
            options.JobExecutionTimeout = TimeSpan.FromMinutes(5);
            options.LogJobExecutions = true;
        })
        .AddSchedulerPlugins(pluginOptions =>
        {
            pluginOptions.EnableAuditPlugin = true;
            pluginOptions.EnableMetricsPlugin = true;
        });

        // Register additional services
        services.AddSingleton<IAlertProvider, ConsoleAlertProvider>();
    });

var host = hostBuilder.Build();

var scheduler = host.Services.GetRequiredService<IScheduler>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var metricsPlugin = host.Services.GetServices<ISchedulerPlugin>().OfType<MetricsPlugin>().FirstOrDefault();

// Subscribe to events
scheduler.JobCompleted += (sender, args) =>
{
    logger.LogInformation("✅ Job completed: {JobName} in {Duration:mm\\:ss\\.fff}", args.JobName, args.Result.Duration);
};

scheduler.JobFailed += (sender, args) =>
{
    logger.LogError("❌ Job failed: {JobName} with error: {Error}", args.JobName, args.Result.Exception?.Message);
};

try
{
    logger.LogInformation("🚀 === Enhanced Scheduler Demo Started ===");

    // Start the enhanced scheduler
    await scheduler.StartAsync();
    logger.LogInformation("📅 Scheduler started successfully");

    // 1. Business Day Job with retry policy
    logger.LogInformation("📝 Scheduling Business Day Job...");
    var job1 = scheduler.ScheduleJob("BusinessDayJob", async (ct) =>
    {
        logger.LogInformation("🏢 Business day job running at {Time:HH:mm:ss}", DateTime.Now);
        await Task.Delay(1000, ct);
        logger.LogInformation("✅ Business day job completed");
    }, new BusinessDayTrigger(TimeSpan.FromHours(9)), new RetryPolicy { MaxRetries = 2 });

    // 2. Analytics Job with exponential backoff retry
    logger.LogInformation("📝 Scheduling Analytics Job...");
    var job2 = scheduler.ScheduleJob("AnalyticsJob", async (ct) =>
    {
        logger.LogInformation("📊 Analytics job collecting data...");
        await Task.Delay(2000, ct);

        // Simulate occasional failure for demo (fails every 3rd second)
        if (DateTime.Now.Second % 3 == 0)
        {
            logger.LogWarning("⚠️ Simulating failure for demo purposes");
            throw new InvalidOperationException("Simulated failure for demo");
        }

        logger.LogInformation("✅ Analytics job completed successfully");
    }, 30.EverySeconds(), new RetryPolicy
    {
        MaxRetries = 3,
        BackoffStrategy = RetryBackoffStrategy.Exponential,
        InitialDelay = TimeSpan.FromSeconds(5)
    });

    // 3. Stateful Job with custom state - FIXED: Using mutable class
    logger.LogInformation("📝 Scheduling Stateful Job...");
    var jobState = new JobState { Counter = 0, LastRun = DateTime.Now };
    var job3 = scheduler.ScheduleJob("StatefulJob", jobState, async (state, ct) =>
    {
        state.Counter++;
        logger.LogInformation("🔢 Stateful job run #{Counter}, LastRun: {LastRun:HH:mm:ss}",
            state.Counter, state.LastRun);
        state.LastRun = DateTime.Now;
        await Task.Delay(1500, ct);
        logger.LogInformation("✅ Stateful job completed");
    }, 45.EverySeconds(), new RetryPolicy { MaxRetries = 2 });

    // 4. Simple Interval Job (using original interface method)
    logger.LogInformation("📝 Scheduling Simple Interval Job...");
    var job4 = scheduler.ScheduleJob("SimpleIntervalJob", async (ct) =>
    {
        logger.LogInformation("⏰ Simple interval job executing...");
        await Task.Delay(800, ct);
        logger.LogInformation("✅ Simple interval job completed");
    }, 60.EverySeconds());

    logger.LogInformation("");
    logger.LogInformation("🎯 Demo Controls:");
    logger.LogInformation("   Press 'm' - View metrics");
    logger.LogInformation("   Press 'a' - View analytics");
    logger.LogInformation("   Press 't' - Trigger BusinessDayJob manually");
    logger.LogInformation("   Press 'j' - List scheduled jobs");
    logger.LogInformation("   Press 'q' - Quit demo");
    logger.LogInformation("");

    while (true)
    {
        var key = Console.ReadKey(intercept: true);

        if (key.KeyChar == 'q')
        {
            logger.LogInformation("🛑 Stopping scheduler...");
            break;
        }

        switch (key.KeyChar)
        {
            case 'm': // View metrics
                if (metricsPlugin != null)
                {
                    var metrics = metricsPlugin.GetAllMetrics();
                    logger.LogInformation("📈 === Current Metrics ===");
                    foreach (var metric in metrics)
                    {
                        logger.LogInformation("   📊 {JobName}:", metric.JobName);
                        logger.LogInformation("      Success Rate: {SuccessRate:P2}", metric.SuccessRate);
                        logger.LogInformation("      Total Executions: {TotalExecutions}", metric.TotalExecutions);
                        logger.LogInformation("      Successful: {Successful}, Failed: {Failed}",
                            metric.SuccessfulExecutions, metric.FailedExecutions);
                        logger.LogInformation("      Avg Duration: {AvgDuration:ss\\.fff}s", metric.AverageDuration);
                        logger.LogInformation("      Last Execution: {LastExecution:HH:mm:ss}", metric.LastExecution);
                    }
                }
                else
                {
                    logger.LogWarning("Metrics plugin not available");
                }
                break;

            case 'a': // View analytics
                try
                {
                    var analytics = await scheduler.GetJobAnalyticsAsync(job2, DateTime.Today.AddDays(-1), DateTime.Now);
                    logger.LogInformation("📊 === Analytics for {JobId} ===", analytics.JobId);
                    logger.LogInformation("   Period: {From:HH:mm:ss} to {To:HH:mm:ss}", analytics.PeriodFrom, analytics.PeriodTo);
                    logger.LogInformation("   Success Rate: {SuccessRate:P2}", analytics.SuccessRate);
                    logger.LogInformation("   Total: {Total}, Successful: {Successful}, Failed: {Failed}",
                        analytics.TotalExecutions, analytics.SuccessfulExecutions, analytics.FailedExecutions);
                    logger.LogInformation("   Avg Duration: {AvgDuration:ss\\.fff}s", analytics.AverageDuration);
                    logger.LogInformation("   Max Duration: {MaxDuration:ss\\.fff}s", analytics.MaxDuration);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to get analytics");
                }
                break;

            case 't': // Trigger job manually
                try
                {
                    logger.LogInformation("⚡ Manually triggering BusinessDayJob...");
                    var success = await scheduler.TriggerJobAsync(job1);
                    logger.LogInformation("✅ Manual trigger result: {Success}", success ? "Success" : "Failed");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to trigger job manually");
                }
                break;

            case 'j': // List scheduled jobs
                var jobs = scheduler.GetScheduledJobs();
                logger.LogInformation("📋 === Scheduled Jobs ({Count}) ===", jobs.Count);
                foreach (var job in jobs)
                {
                    logger.LogInformation("   🆔 {JobName} ({Status})", job.Name, job.Status);
                    logger.LogInformation("      Next Run: {NextRun:HH:mm:ss}", job.NextRun);
                    logger.LogInformation("      Last Run: {LastRun:HH:mm:ss}", job.LastRun);
                }
                break;

            default:
                logger.LogInformation("❓ Unknown command. Press 'q' to quit.");
                break;
        }
    }

    await scheduler.StopAsync();
    logger.LogInformation("✅ Enhanced scheduler demo completed successfully!");
}
catch (Exception ex)
{
    logger.LogError(ex, "💥 Enhanced demo failed");
}
finally
{
    await host.StopAsync();
    logger.LogInformation("🔚 Demo shut down complete");
}

// State class for stateful jobs
public class JobState
{
    public int Counter { get; set; }
    public DateTime LastRun { get; set; }
}