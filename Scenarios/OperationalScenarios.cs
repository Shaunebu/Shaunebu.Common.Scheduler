using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shaunebu.Common.Scheduler.Abstractions;
using Shaunebu.Common.Scheduler.Client.Infrastructure;
using Shaunebu.Common.Scheduler.Client.Jobs;
using Shaunebu.Common.Scheduler.Enums;
using Shaunebu.Common.Scheduler.Extensions;
using Shaunebu.Common.Scheduler.Models;
using Shaunebu.Common.Scheduler.Persistence;
using Shaunebu.Common.Scheduler.Plugins;
using Shaunebu.Common.Scheduler.Services;
using Shaunebu.Common.Scheduler.Triggers;

namespace Shaunebu.Common.Scheduler.Client.Scenarios;

internal sealed class ConcurrencyScenario : ScenarioBase
{
    public ConcurrencyScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "concurrency";
    public override string Name => "Concurrency";
    public override string Description => "Shows MaxConcurrentJobs limiting simultaneous executions across different jobs.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider(options => options.MaxConcurrentJobs = 2);
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var active = 0;
        var maxObserved = 0;
        var entered = 0;
        var twoEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task Job(CancellationToken token)
        {
            var current = Interlocked.Increment(ref active);
            UpdateMax(ref maxObserved, current);
            if (Interlocked.Increment(ref entered) == 2)
            {
                twoEntered.TrySetResult();
            }

            try
            {
                await release.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }

        var first = scheduler.ScheduleJob("Concurrent A", Job, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));
        var second = scheduler.ScheduleJob("Concurrent B", Job, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));
        var third = scheduler.ScheduleJob("Concurrent C", Job, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));

        var firstTask = scheduler.TriggerJobAsync(first);
        var secondTask = scheduler.TriggerJobAsync(second);
        await twoEntered.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        var thirdResult = await scheduler.TriggerJobAsync(third).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);

        release.SetResult();
        var firstResult = await firstTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        var secondResult = await secondTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        Output.KeyValue("Configured MaxConcurrentJobs", 2);
        Output.KeyValue("First job result", firstResult);
        Output.KeyValue("Second job result", secondResult);
        Output.KeyValue("Third while saturated", thirdResult);
        Output.KeyValue("Observed max active", maxObserved);

        Require(firstResult && secondResult, "The first two concurrent jobs should complete.");
        Require(!thirdResult, "The third job should be skipped while the concurrency limiter is saturated.");
        Require(maxObserved <= 2, "Observed concurrency exceeded MaxConcurrentJobs.");
    }

    private static void UpdateMax(ref int maxObserved, int current)
    {
        while (true)
        {
            var snapshot = Volatile.Read(ref maxObserved);
            if (current <= snapshot || Interlocked.CompareExchange(ref maxObserved, current, snapshot) == snapshot)
            {
                return;
            }
        }
    }
}

internal sealed class SameJobOverlapScenario : ScenarioBase
{
    public SameJobOverlapScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "overlap";
    public override string Name => "Same-Job Overlap Protection";
    public override string Description => "Triggers the same job while it is already running and shows simultaneous executions stay at one.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider(options => options.MaxConcurrentJobs = 2);
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var active = 0;
        var maxSameJobActive = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var jobId = scheduler.ScheduleJob(
            "No overlap",
            async token =>
            {
                var current = Interlocked.Increment(ref active);
                Volatile.Write(ref maxSameJobActive, Math.Max(Volatile.Read(ref maxSameJobActive), current));
                entered.TrySetResult();
                try
                {
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(2), token);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            },
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));

        var firstTask = scheduler.TriggerJobAsync(jobId);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        var secondResult = await scheduler.TriggerJobAsync(jobId).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        release.SetResult();
        var firstResult = await firstTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);

        Output.KeyValue("First trigger", firstResult);
        Output.KeyValue("Second trigger while running", secondResult);
        Output.KeyValue("Observed same-job max", maxSameJobActive);

        Require(firstResult, "Initial job execution should complete.");
        Require(!secondResult, "Second trigger should not overlap the same job.");
        Require(maxSameJobActive == 1, "The same job overlapped with itself.");
    }
}

internal sealed class ScopedJobsScenario : ScenarioBase
{
    public ScopedJobsScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "scoped-jobs";
    public override string Name => "Scoped / Typed Jobs";
    public override string Description => "Schedules an IJob through IScopedJobScheduler and shows fresh DI scopes per retry attempt.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider();
        var scopedScheduler = provider.GetRequiredService<IScopedJobScheduler>();
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var recorder = provider.GetRequiredService<ScopedExecutionRecorder>();

        var jobId = scopedScheduler.ScheduleJob<FlakyScopedJob>(
            "Flaky scoped job",
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)),
            new RetryPolicy { MaxRetries = 1, InitialDelay = TimeSpan.Zero });

        var result = await scheduler.TriggerJobAsync(jobId).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        var dependencyIds = recorder.DependencyIds.ToArray();

        Output.KeyValue("Typed job ID", jobId);
        Output.KeyValue("Execution result", result);
        Output.KeyValue("Attempts", recorder.Attempts);
        Output.KeyValue("Attempt 1 scope", dependencyIds.ElementAtOrDefault(0));
        Output.KeyValue("Attempt 2 scope", dependencyIds.ElementAtOrDefault(1));
        Output.KeyValue("Disposed scopes", recorder.DisposedDependencyIds.Count);

        Require(result, "Scoped retry job should succeed on the second attempt.");
        Require(dependencyIds.Length == 2, "Scoped retry should have two attempts.");
        Require(dependencyIds[0] != dependencyIds[1], "Each scoped retry attempt should use a fresh dependency instance.");
    }
}

internal sealed class DependencyInjectionScenario : ScenarioBase
{
    public DependencyInjectionScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "dependency-injection";
    public override string Name => "Dependency Injection";
    public override string Description => "Resolves scheduler services registered by AddShaunebuScheduler and verifies singleton aliases.";

    public override Task RunAsync(CancellationToken cancellationToken)
    {
        using var provider = DemoSchedulerFactory.CreateProvider(options =>
        {
            options.MaxConcurrentJobs = 3;
            options.TimeProvider = TimeProvider.System;
        });

        var schedulerService = provider.GetRequiredService<SchedulerService>();
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var stable = provider.GetRequiredService<IStableJobScheduler>();
        var scoped = provider.GetRequiredService<IScopedJobScheduler>();
        var options = provider.GetRequiredService<SchedulerOptions>();

        Output.KeyValue("SchedulerService resolved", schedulerService.GetType().Name);
        Output.KeyValue("IScheduler same singleton", ReferenceEquals(schedulerService, scheduler));
        Output.KeyValue("IStableJobScheduler same singleton", ReferenceEquals(schedulerService, stable));
        Output.KeyValue("IScopedJobScheduler resolved", scoped.GetType().Name);
        Output.KeyValue("MaxConcurrentJobs option", options.MaxConcurrentJobs);
        Output.KeyValue("TimeProvider option", ReferenceEquals(options.TimeProvider, TimeProvider.System) ? "TimeProvider.System" : options.TimeProvider.GetType().Name);

        Require(ReferenceEquals(schedulerService, scheduler), "IScheduler should resolve to the SchedulerService singleton.");
        Require(ReferenceEquals(schedulerService, stable), "IStableJobScheduler should resolve to the SchedulerService singleton.");
        Require(scoped is not null, "IScopedJobScheduler was not resolved.");
        return Task.CompletedTask;
    }
}

internal sealed class GenericHostScenario : ScenarioBase
{
    public GenericHostScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "generic-host";
    public override string Name => "Generic Host Integration";
    public override string Description => "Uses AddShaunebuSchedulerHostedService so host start/stop controls the scheduler singleton.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
        builder.Logging.SetMinimumLevel(LogLevel.Critical);
        builder.Services.AddShaunebuScheduler(options =>
        {
            options.MaxConcurrentJobs = 2;
            options.ShutdownTimeout = TimeSpan.FromSeconds(2);
        });
        builder.Services.AddShaunebuSchedulerHostedService();

        using var host = builder.Build();
        var scheduler = host.Services.GetRequiredService<global::IScheduler>();
        var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var jobId = scheduler.ScheduleJob("Hosted job", _ =>
        {
            executed.TrySetResult();
            return Task.CompletedTask;
        }, new RunOnceImmediatelyTrigger());

        await host.StartAsync(cancellationToken);
        await executed.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        await host.StopAsync(cancellationToken);

        var job = scheduler.GetScheduledJobs().Single(item => item.Id == jobId);
        Output.KeyValue("Host started scheduler", true);
        Output.KeyValue("Hosted job ID", jobId);
        Output.KeyValue("Job status", job.Status);
        Output.KeyValue("Host stopped scheduler", true);

        Require(job.Status == JobStatus.Completed, "Hosted scheduler job did not complete.");
    }
}

internal sealed class LifecycleTimeProviderLoggingScenario : ScenarioBase
{
    public LifecycleTimeProviderLoggingScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "lifecycle";
    public override string Name => "Lifecycle, TimeProvider, and Logging";
    public override string Description => "Shows manual StartAsync/StopAsync plus TimeProvider.System and LogJobExecutions options.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider(options =>
        {
            options.TimeProvider = TimeProvider.System;
            options.LogJobExecutions = false;
        });

        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var options = provider.GetRequiredService<SchedulerOptions>();

        await scheduler.StartAsync(cancellationToken);
        await scheduler.StopAsync(cancellationToken);

        Output.KeyValue("Manual StartAsync", "completed");
        Output.KeyValue("Manual StopAsync", "completed");
        Output.KeyValue("Configured TimeProvider", ReferenceEquals(options.TimeProvider, TimeProvider.System) ? "TimeProvider.System" : options.TimeProvider.GetType().Name);
        Output.KeyValue("LogJobExecutions", options.LogJobExecutions);
        Output.Line("Scheduler polling, runtime timestamps, cleanup waits, and retry waits use SchedulerOptions.TimeProvider.");

        Require(ReferenceEquals(options.TimeProvider, TimeProvider.System), "TimeProvider.System was not configured.");
    }
}

internal sealed class PluginsMetricsScenario : ScenarioBase
{
    public PluginsMetricsScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "observability";
    public override string Name => "Plugins and Metrics";
    public override string Description => "Demonstrates plugin callbacks, failure isolation, AuditPlugin logging, and MetricsPlugin snapshots.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        var recordingPlugin = new RecordingSchedulerPlugin();
        await using var provider = DemoSchedulerFactory.CreateProvider(
            configureServices: services =>
            {
                services.AddSingleton<ISchedulerPlugin>(recordingPlugin);
                services.AddSingleton<ISchedulerPlugin, ThrowingSchedulerPlugin>();
                services.AddSchedulerPlugins(options =>
                {
                    options.EnableAuditPlugin = true;
                    options.EnableMetricsPlugin = true;
                });
            });

        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var metrics = provider.GetServices<ISchedulerPlugin>().OfType<MetricsPlugin>().Single();
        var jobId = scheduler.ScheduleJob("Observed job", _ => Task.CompletedTask, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));

        await scheduler.StartAsync(cancellationToken);
        var result = await scheduler.TriggerJobAsync(jobId).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        var snapshot = metrics.GetJobMetrics(jobId);
        var unscheduled = scheduler.UnscheduleJob(jobId);
        await scheduler.StopAsync(cancellationToken);

        Output.Section("Plugin callbacks");
        foreach (var item in recordingPlugin.Events)
        {
            Output.Line($"  {item}");
        }

        Output.Section("Metrics snapshot");
        Output.KeyValue("Total executions", snapshot?.TotalExecutions);
        Output.KeyValue("Successful executions", snapshot?.SuccessfulExecutions);
        Output.KeyValue("Failed executions", snapshot?.FailedExecutions);
        Output.KeyValue("Total duration", snapshot?.TotalDuration);
        Output.KeyValue("Max duration", snapshot?.MaxDuration);
        Output.KeyValue("Last execution", snapshot?.LastExecution.ToString("O"));
        Output.KeyValue("Throwing plugin isolated", result && unscheduled);

        Require(result, "Observed job did not execute.");
        Require(snapshot?.TotalExecutions == 1 && snapshot.SuccessfulExecutions == 1, "MetricsPlugin did not record the successful execution.");
        Require(recordingPlugin.Events.Any(item => item.Contains("scheduler starting", StringComparison.Ordinal)), "Scheduler starting callback was not observed.");
        Require(recordingPlugin.Events.Any(item => item.Contains("job executed", StringComparison.Ordinal)), "Job executed callback was not observed.");
    }
}

internal sealed class ExecutionHistoryAlertsScenario : ScenarioBase
{
    public ExecutionHistoryAlertsScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "history-alerts";
    public override string Name => "Execution History and Alerts";
    public override string Description => "Records success, retried success, exhausted failure, final-failure alerting, and alert failure isolation.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        var alertProvider = new RecordingAlertProvider();
        await using var provider = DemoSchedulerFactory.CreateProvider(configureServices: services => services.AddSingleton<IAlertProvider>(alertProvider));
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var historyRepository = provider.GetRequiredService<InMemoryJobHistoryRepository>();
        var retryAttempts = 0;
        var failAttempts = 0;

        var successId = scheduler.ScheduleJob("History success", _ => Task.CompletedTask, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));
        var retryId = scheduler.ScheduleJob(
            "History retried",
            _ =>
            {
                if (Interlocked.Increment(ref retryAttempts) == 1)
                {
                    throw new InvalidOperationException("First history attempt fails.");
                }

                return Task.CompletedTask;
            },
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)),
            new RetryPolicy { MaxRetries = 1, InitialDelay = TimeSpan.Zero });
        var failId = scheduler.ScheduleJob(
            "History final failure",
            _ =>
            {
                Interlocked.Increment(ref failAttempts);
                throw new InvalidOperationException("Final failure for alert demo.");
            },
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)),
            new RetryPolicy { MaxRetries = 1, InitialDelay = TimeSpan.Zero });

        var successResult = await scheduler.TriggerJobAsync(successId).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        var retryResult = await scheduler.TriggerJobAsync(retryId).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        var failResult = await scheduler.TriggerJobAsync(failId).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);

        var successHistory = (await historyRepository.GetJobHistoryAsync(successId, DateTime.MinValue, DateTime.MaxValue)).ToArray();
        var retryHistory = (await historyRepository.GetJobHistoryAsync(retryId, DateTime.MinValue, DateTime.MaxValue)).OrderBy(item => item.RetryCount).ToArray();
        var failHistory = (await historyRepository.GetJobHistoryAsync(failId, DateTime.MinValue, DateTime.MaxValue)).OrderBy(item => item.RetryCount).ToArray();
        var recentFailures = (await historyRepository.GetRecentFailuresAsync()).ToArray();

        Output.KeyValue("Success result", successResult);
        Output.KeyValue("Retried result", retryResult);
        Output.KeyValue("Final failure result", failResult);
        Output.KeyValue("Success history records", successHistory.Length);
        Output.KeyValue("Retried history", string.Join(", ", retryHistory.Select(item => $"retry={item.RetryCount}, success={item.Success}")));
        Output.KeyValue("Failure history", string.Join(", ", failHistory.Select(item => $"retry={item.RetryCount}, success={item.Success}")));
        Output.KeyValue("Recent failures", recentFailures.Length);
        Output.KeyValue("Final-failure alerts", alertProvider.Messages.Count);

        await DemonstrateThrowingAlertProviderAsync(cancellationToken);

        Require(successResult && retryResult && !failResult, "History job outcomes were unexpected.");
        Require(successHistory.Length == 1 && retryHistory.Length == 2 && failHistory.Length == 2, "History record counts were unexpected.");
        Require(alertProvider.Messages.Count == 1, "Final failure should emit one alert.");
    }

    private async Task DemonstrateThrowingAlertProviderAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider(configureServices: services => services.AddSingleton<IAlertProvider, ThrowingAlertProvider>());
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var jobId = scheduler.ScheduleJob(
            "Throwing alert provider",
            _ => throw new InvalidOperationException("Alert provider isolation demo."),
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)),
            new RetryPolicy { MaxRetries = 0, InitialDelay = TimeSpan.Zero });

        var result = await scheduler.TriggerJobAsync(jobId).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        Output.KeyValue("Throwing alert isolated", !result);
    }
}

internal sealed class GuardrailsInspectionScenario : ScenarioBase
{
    public GuardrailsInspectionScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "guardrails";
    public override string Name => "Guardrails and Scheduler Inspection";
    public override string Description => "Demonstrates MaxScheduledJobs, invalid configuration rejection, unscheduling, and public inspection snapshots.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider(options => options.MaxScheduledJobs = 2);
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var stableScheduler = provider.GetRequiredService<IStableJobScheduler>();

        var first = scheduler.ScheduleJob("Capacity A", _ => Task.CompletedTask, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));
        var second = scheduler.ScheduleJob("Capacity B", _ => Task.CompletedTask, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));
        var thirdRejected = Catch<InvalidOperationException>(() =>
            scheduler.ScheduleJob("Capacity C", _ => Task.CompletedTask, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1))));
        var unscheduled = scheduler.UnscheduleJob(first);
        var third = scheduler.ScheduleJob("Capacity C", _ => Task.CompletedTask, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));
        var invalidStableId = Catch<ArgumentException>(() =>
            stableScheduler.ScheduleJob(" ", "Invalid stable", _ => Task.CompletedTask, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1))));

        var invalidMaxConcurrent = Catch<ArgumentOutOfRangeException>(() =>
        {
            using var invalidProvider = DemoSchedulerFactory.CreateProvider(options => options.MaxConcurrentJobs = 0);
            _ = invalidProvider.GetRequiredService<global::IScheduler>();
        });
        var invalidMaxScheduled = Catch<ArgumentOutOfRangeException>(() =>
        {
            using var invalidProvider = DemoSchedulerFactory.CreateProvider(options => options.MaxScheduledJobs = 0);
            _ = invalidProvider.GetRequiredService<global::IScheduler>();
        });
        var invalidTimeout = Catch<ArgumentOutOfRangeException>(() =>
        {
            using var invalidProvider = DemoSchedulerFactory.CreateProvider(options => options.ShutdownTimeout = TimeSpan.FromMilliseconds(-1));
            _ = invalidProvider.GetRequiredService<global::IScheduler>();
        });
        var invalidInterval = Catch<ArgumentException>(() => _ = new IntervalTrigger(TimeSpan.Zero));
        var invalidCron = Catch<ArgumentException>(() => _ = new CronTrigger("definitely invalid"));

        var jobs = scheduler.GetScheduledJobs().OrderBy(job => job.Name).ToArray();

        Output.KeyValue("MaxScheduledJobs", 2);
        Output.KeyValue("First job ID", first);
        Output.KeyValue("Second job ID", second);
        Output.KeyValue("Third rejected at capacity", thirdRejected);
        Output.KeyValue("Unschedule freed capacity", unscheduled);
        Output.KeyValue("Third scheduled after free", third);
        Output.KeyValue("Invalid stable ID rejected", invalidStableId);
        Output.KeyValue("Invalid MaxConcurrentJobs", invalidMaxConcurrent);
        Output.KeyValue("Invalid MaxScheduledJobs", invalidMaxScheduled);
        Output.KeyValue("Invalid ShutdownTimeout", invalidTimeout);
        Output.KeyValue("Invalid interval", invalidInterval);
        Output.KeyValue("Invalid cron", invalidCron);

        Output.Section("Inspection snapshot");
        foreach (var job in jobs)
        {
            Output.Line($"  {job.Id} | {job.Name} | {job.Status} | NextRun={job.NextRun?.ToString("O")}");
        }

        Require(thirdRejected && invalidStableId && invalidMaxConcurrent && invalidMaxScheduled && invalidTimeout && invalidInterval && invalidCron, "One or more guardrails did not reject invalid input.");
        Require(unscheduled && scheduler.IsJobScheduled(third), "Capacity should become available after unscheduling.");
        await Task.CompletedTask;
    }

    private static bool Catch<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}

internal sealed class SchedulingSemanticsScenario : ScenarioBase
{
    public SchedulingSemanticsScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "semantics";
    public override string Name => "Scheduling Semantics";
    public override string Description => "Shows public event trigger calculations and documents semantics that remain covered by automated tests.";

    public override Task RunAsync(CancellationToken cancellationToken)
    {
        var eventBus = new SimpleEventBus();
        var eventTrigger = new EventTrigger("catalog-ready", eventBus);
        var from = TimeProvider.System.GetUtcNow();
        var beforePublish = eventTrigger.GetNextOccurrence(from);
        eventBus.Publish("catalog-ready", new { Batch = 17 });
        var afterPublish = eventTrigger.GetNextOccurrence(from.AddSeconds(1));
        var afterReset = eventTrigger.GetNextOccurrence(from.AddSeconds(2));

        Output.KeyValue("Event before publish", beforePublish?.ToString("O") ?? "null");
        Output.KeyValue("Event after publish", afterPublish?.ToString("O") ?? "null");
        Output.KeyValue("Event after reset", afterReset?.ToString("O") ?? "null");
        Output.Line("Missed recurring occurrences are covered by automated scheduler tests; this client avoids a timing-sensitive live replay demo.");
        Output.Line("ChainedTrigger exposes public trigger semantics, but live Job A -> Job B execution is not demonstrated here because scheduled jobs snapshot NextRun when scheduled and no public refresh hook is exposed.");

        Require(beforePublish is null, "Event trigger should not be due before the event.");
        Require(afterPublish.HasValue, "Event trigger should become due after the event.");
        Require(afterReset is null, "Event trigger should reset after one due occurrence.");
        return Task.CompletedTask;
    }
}

internal sealed class KnownLimitationsScenario : ScenarioBase
{
    public KnownLimitationsScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "limitations";
    public override string Name => "Known Limitations";
    public override string Description => "States current 1.1.0 package boundaries without presenting placeholder APIs as working integrations.";

    public override Task RunAsync(CancellationToken cancellationToken)
    {
        Output.Line("The current package is an in-process, single-process scheduler.");
        Output.Line("This client does not present the following as working package features:");
        Output.Line("  distributed execution");
        Output.Line("  cluster coordination or leader election");
        Output.Line("  exactly-once execution across replicas");
        Output.Line("  durable executable-job recovery or delegate reconstruction after restart");
        Output.Line("  SQL persistence implementation");
        Output.Line("  Redis distributed locking implementation");
        Output.Line("  Slack, Prometheus, OpenTelemetry, or Application Insights exporters");
        Output.Line("  email alert provider implementation");
        return Task.CompletedTask;
    }
}
