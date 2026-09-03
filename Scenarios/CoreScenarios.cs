using Microsoft.Extensions.DependencyInjection;
using Shaunebu.Common.Scheduler.Abstractions;
using Shaunebu.Common.Scheduler.Client.Infrastructure;
using Shaunebu.Common.Scheduler.Enums;
using Shaunebu.Common.Scheduler.Models;
using Shaunebu.Common.Scheduler.Persistence;
using Shaunebu.Common.Scheduler.Triggers;

namespace Shaunebu.Common.Scheduler.Client.Scenarios;

internal sealed class BasicSchedulingScenario : ScenarioBase
{
    public BasicSchedulingScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "basic";
    public override string Name => "Basic Scheduling";
    public override string Description => "Resolves IScheduler, schedules a delegate, starts the runtime, executes it, and stops cleanly.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider();
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var jobId = scheduler.ScheduleJob(
            "Basic delegate job",
            _ =>
            {
                executed.TrySetResult();
                return Task.CompletedTask;
            },
            new RunOnceImmediatelyTrigger());

        Output.KeyValue("Generated job ID", jobId);
        Output.KeyValue("Scheduled before start", scheduler.IsJobScheduled(jobId));

        await scheduler.StartAsync(cancellationToken);
        await executed.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        await scheduler.StopAsync(cancellationToken);

        var job = scheduler.GetScheduledJobs().Single(item => item.Id == jobId);
        Output.KeyValue("Status", job.Status);
        Output.KeyValue("NextRun after execution", job.NextRun?.ToString("O") ?? "null");

        Require(job.Status == JobStatus.Completed, "The basic job did not complete.");
        Require(job.NextRun is null, "The run-once trigger should have no next run after completion.");
    }
}

internal sealed class OneTimeScenario : ScenarioBase
{
    public OneTimeScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "one-time";
    public override string Name => "One-Time Jobs";
    public override string Description => "Demonstrates future one-shot execution, retained completion state, and past-trigger eligibility.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider();
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var attempts = 0;

        var runAt = TimeProvider.System.GetUtcNow();
        var jobId = scheduler.ScheduleJob(
            "One-time report",
            _ =>
            {
                Interlocked.Increment(ref attempts);
                return Task.CompletedTask;
            },
            new OneTimeTrigger(runAt));

        var before = scheduler.GetScheduledJobs().Single(job => job.Id == jobId);
        Output.KeyValue("RunAt", runAt.ToString("O"));
        Output.KeyValue("NextRun before execution", before.NextRun?.ToString("O"));

        var executed = await scheduler.TriggerJobAsync(jobId);
        var after = scheduler.GetScheduledJobs().Single(job => job.Id == jobId);
        var pastTrigger = new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddMilliseconds(-1));

        Output.KeyValue("Manual execution result", executed);
        Output.KeyValue("Retained completed job", scheduler.IsJobScheduled(jobId));
        Output.KeyValue("Status", after.Status);
        Output.KeyValue("NextRun after completion", after.NextRun?.ToString("O") ?? "null");
        Output.KeyValue("Past trigger next run", pastTrigger.GetNextOccurrence(TimeProvider.System.GetUtcNow())?.ToString("O") ?? "null");

        Require(executed, "One-time job did not execute.");
        Require(Volatile.Read(ref attempts) == 1, "One-time job executed an unexpected number of times.");
        Require(after.Status == JobStatus.Completed, "One-time job should be completed.");
        Require(after.NextRun is null, "One-time job should not have a future NextRun after completion.");
    }
}

internal sealed class StableIdsScenario : ScenarioBase
{
    public StableIdsScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "stable-ids";
    public override string Name => "Stable Job IDs";
    public override string Description => "Schedules with caller-supplied identity, rejects duplicates, manually triggers, and unschedules.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider();
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var stableScheduler = provider.GetRequiredService<IStableJobScheduler>();
        var executions = 0;
        const string stableId = "nightly-cleanup";

        var returnedId = stableScheduler.ScheduleJob(
            stableId,
            "Nightly cleanup",
            _ =>
            {
                Interlocked.Increment(ref executions);
                return Task.CompletedTask;
            },
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));

        var duplicateRejected = false;
        try
        {
            stableScheduler.ScheduleJob(stableId, "Duplicate cleanup", _ => Task.CompletedTask, new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }

        var triggered = await scheduler.TriggerJobAsync(stableId);
        var unscheduled = scheduler.UnscheduleJob(stableId);

        Output.KeyValue("Caller-supplied ID", stableId);
        Output.KeyValue("Returned ID", returnedId);
        Output.KeyValue("Is scheduled", scheduler.IsJobScheduled(stableId));
        Output.KeyValue("Duplicate rejected", duplicateRejected);
        Output.KeyValue("Manual trigger", triggered);
        Output.KeyValue("Unschedule result", unscheduled);
        Output.KeyValue("Is scheduled after unschedule", scheduler.IsJobScheduled(stableId));

        Require(returnedId == stableId, "Stable scheduler did not return the supplied ID.");
        Require(duplicateRejected, "Duplicate stable ID was not rejected.");
        Require(triggered, "Stable job did not manually trigger.");
        Require(Volatile.Read(ref executions) == 1, "Stable job execution count was unexpected.");
        Require(unscheduled && !scheduler.IsJobScheduled(stableId), "Stable job did not unschedule.");
    }
}

internal sealed class StatefulJobsScenario : ScenarioBase
{
    public StatefulJobsScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "stateful";
    public override string Name => "Stateful Jobs";
    public override string Description => "Uses the public stateful delegate overload to pass a typed state object into execution.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider();
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var state = new ReportState(42, "north-region");
        ReportState? observed = null;

        var jobId = scheduler.ScheduleJob(
            "Stateful report",
            state,
            (currentState, _) =>
            {
                observed = currentState;
                return Task.CompletedTask;
            },
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)));

        var triggered = await scheduler.TriggerJobAsync(jobId);

        Output.KeyValue("Job ID", jobId);
        Output.KeyValue("Report ID", observed?.ReportId);
        Output.KeyValue("Region", observed?.Region);
        Output.KeyValue("Triggered", triggered);

        Require(triggered, "Stateful job did not trigger.");
        Require(ReferenceEquals(state, observed), "State object was not supplied to the delegate.");
    }

    private sealed record ReportState(int ReportId, string Region);
}

internal sealed class ManualTriggeringScenario : ScenarioBase
{
    public ManualTriggeringScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "manual";
    public override string Name => "Manual Triggering";
    public override string Description => "Triggers a recurring job through TriggerJobAsync and inspects history without consuming the future occurrence.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider();
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var historyRepository = provider.GetRequiredService<InMemoryJobHistoryRepository>();
        var nextScheduled = TimeProvider.System.GetUtcNow().AddMinutes(5);
        var executions = 0;

        var jobId = scheduler.ScheduleJob(
            "Manual interval",
            _ =>
            {
                Interlocked.Increment(ref executions);
                return Task.CompletedTask;
            },
            new IntervalTrigger(TimeSpan.FromMinutes(5), nextScheduled));

        var before = scheduler.GetScheduledJobs().Single(job => job.Id == jobId);
        var triggered = await scheduler.TriggerJobAsync(jobId);
        var after = scheduler.GetScheduledJobs().Single(job => job.Id == jobId);
        var history = (await historyRepository.GetJobHistoryAsync(jobId, DateTime.MinValue, DateTime.MaxValue)).ToArray();
        var analytics = await scheduler.GetJobAnalyticsAsync(jobId, DateTime.MinValue, DateTime.MaxValue);

        Output.KeyValue("NextRun before manual trigger", before.NextRun?.ToString("O"));
        Output.KeyValue("Triggered", triggered);
        Output.KeyValue("Executions", Volatile.Read(ref executions));
        Output.KeyValue("NextRun after manual trigger", after.NextRun?.ToString("O"));
        Output.KeyValue("History records", history.Length);
        Output.KeyValue("Analytics success rate", analytics.SuccessRate.ToString("P0"));

        Require(triggered, "Manual trigger did not execute.");
        Require(after.NextRun == before.NextRun, "Manual trigger advanced the recurring NextRun unexpectedly.");
        Require(history.Length == 1 && analytics.TotalExecutions == 1, "Execution history was not recorded.");
    }
}
