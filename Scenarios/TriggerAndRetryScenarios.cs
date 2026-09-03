using Microsoft.Extensions.DependencyInjection;
using Shaunebu.Common.Scheduler.Client.Infrastructure;
using Shaunebu.Common.Scheduler.Enums;
using Shaunebu.Common.Scheduler.Extensions;
using Shaunebu.Common.Scheduler.Models;
using Shaunebu.Common.Scheduler.Triggers;

namespace Shaunebu.Common.Scheduler.Client.Scenarios;

internal sealed class IntervalScenario : ScenarioBase
{
    public IntervalScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "interval";
    public override string Name => "Interval Jobs";
    public override string Description => "Runs a short interval job more than once and shuts it down explicitly.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider();
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var executions = 0;
        var secondExecution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var jobId = scheduler.ScheduleJob(
            "Interval heartbeat",
            _ =>
            {
                if (Interlocked.Increment(ref executions) >= 2)
                {
                    secondExecution.TrySetResult();
                }

                return Task.CompletedTask;
            },
            new IntervalTrigger(TimeSpan.FromMilliseconds(250), TimeProvider.System.GetUtcNow()));

        await scheduler.StartAsync(cancellationToken);
        await secondExecution.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
        await scheduler.StopAsync(cancellationToken);
        var unscheduled = scheduler.UnscheduleJob(jobId);

        Output.KeyValue("Job ID", jobId);
        Output.KeyValue("Executions observed", Volatile.Read(ref executions));
        Output.KeyValue("Unscheduled", unscheduled);

        Require(Volatile.Read(ref executions) >= 2, "Interval job did not execute multiple times.");
        Require(unscheduled, "Interval job did not unschedule.");
    }
}

internal sealed class CalendarTriggerScenario : ScenarioBase
{
    public CalendarTriggerScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "calendar";
    public override string Name => "Calendar, Cron, and Trigger Extensions";
    public override string Description => "Calculates next occurrences for daily, business-day, cron, time-zone, and extension triggers.";

    public override Task RunAsync(CancellationToken cancellationToken)
    {
        var utc = TimeZoneInfo.Utc;
        var local = TimeZoneInfo.Local;
        var from = new DateTimeOffset(2026, 1, 2, 15, 30, 0, TimeSpan.Zero);
        var daily = new DailyTrigger(new TimeSpan(9, 0, 0), utc);
        var businessDay = new BusinessDayTrigger(new TimeSpan(10, 0, 0), utc);
        var fiveFieldCron = new CronTrigger("*/15 * * * *", utc);
        var sixFieldCron = new CronTrigger("*/10 * * * * *", utc);
        var extensionInterval = 5.EveryMinutes();
        var extensionCron = "0 9 * * 1-5".Cron(utc);
        var extensionDaily = new TimeSpan(8, 45, 0).DailyAt(utc);
        var extensionOnceAt = from.AddMinutes(1).RunOnceAt();
        var extensionOnceIn = TimeSpan.FromMinutes(1).RunOnceIn();

        var invalidCronRejected = false;
        try
        {
            _ = new CronTrigger("not valid cron");
        }
        catch (ArgumentException)
        {
            invalidCronRejected = true;
        }

        Output.Section("Direct trigger calculations");
        Output.KeyValue("Daily UTC", daily.GetNextOccurrence(from)?.ToString("O"));
        Output.KeyValue("Business day UTC", businessDay.GetNextOccurrence(from)?.ToString("O"));
        Output.KeyValue("Five-field cron", fiveFieldCron.GetNextOccurrence(from)?.ToString("O"));
        Output.KeyValue("Six-field cron", sixFieldCron.GetNextOccurrence(from)?.ToString("O"));
        Output.KeyValue("Local time zone", local.Id);
        Output.KeyValue("UTC conversion source", from.ToString("O"));

        Output.Section("Extension APIs");
        Output.KeyValue("5.EveryMinutes()", extensionInterval.Description);
        Output.KeyValue("\"0 9 * * 1-5\".Cron()", extensionCron.Description);
        Output.KeyValue("TimeSpan.DailyAt()", extensionDaily.Description);
        Output.KeyValue("DateTimeOffset.RunOnceAt()", extensionOnceAt.Description);
        Output.KeyValue("TimeSpan.RunOnceIn()", extensionOnceIn.Description);
        Output.KeyValue("Invalid cron rejected", invalidCronRejected);
        Output.Line("Cron parsing uses NCrontab semantics; Quartz-specific syntax is not implied.");

        Require(daily.GetNextOccurrence(from).HasValue, "Daily trigger did not calculate a next run.");
        Require(businessDay.GetNextOccurrence(from).HasValue, "Business-day trigger did not calculate a next run.");
        Require(fiveFieldCron.GetNextOccurrence(from).HasValue, "Five-field cron did not calculate a next run.");
        Require(sixFieldCron.GetNextOccurrence(from).HasValue, "Six-field cron did not calculate a next run.");
        Require(invalidCronRejected, "Invalid cron expression was not rejected.");
        return Task.CompletedTask;
    }
}

internal sealed class RetryPoliciesScenario : ScenarioBase
{
    public RetryPoliciesScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "retry";
    public override string Name => "Retry Policies";
    public override string Description => "Exercises fixed, linear, and exponential retry backoff and prints retry attempt counts.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider();
        var scheduler = provider.GetRequiredService<global::IScheduler>();

        await RunRetryCaseAsync(scheduler, "Fixed", RetryBackoffStrategy.Fixed, cancellationToken);
        await RunRetryCaseAsync(scheduler, "Linear", RetryBackoffStrategy.Linear, cancellationToken);
        await RunRetryCaseAsync(scheduler, "Exponential", RetryBackoffStrategy.Exponential, cancellationToken);

        Output.Line();
        Output.Line("MaxRetries = 2 means 1 initial attempt + up to 2 retries = maximum 3 attempts.");
    }

    private async Task RunRetryCaseAsync(
        global::IScheduler scheduler,
        string label,
        RetryBackoffStrategy strategy,
        CancellationToken cancellationToken)
    {
        var attempts = 0;
        var policy = new RetryPolicy
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(5),
            BackoffStrategy = strategy
        };

        var jobId = scheduler.ScheduleJob(
            $"{label} retry",
            _ =>
            {
                var attempt = Interlocked.Increment(ref attempts);
                Output.Line($"  [{label}] attempt {attempt}");
                if (attempt < 3)
                {
                    throw new InvalidOperationException($"{label} attempt {attempt} failed intentionally.");
                }

                return Task.CompletedTask;
            },
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)),
            policy);

        var result = await scheduler.TriggerJobAsync(jobId).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        var job = scheduler.GetScheduledJobs().Single(item => item.Id == jobId);

        Output.KeyValue($"{label} result", $"{result}, attempts={attempts}, status={job.Status}");
        Require(result, $"{label} retry job did not eventually succeed.");
        Require(Volatile.Read(ref attempts) == 3, $"{label} retry attempt count was unexpected.");
    }
}

internal sealed class RetryFilteringScenario : ScenarioBase
{
    public RetryFilteringScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "retry-filter";
    public override string Name => "Retry Exception Filtering";
    public override string Description => "Uses ShouldRetryException to retry TimeoutException and stop on InvalidOperationException.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider();
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var timeoutAttempts = 0;
        var invalidAttempts = 0;

        var timeoutJobId = scheduler.ScheduleJob(
            "Retry timeout only",
            _ =>
            {
                if (Interlocked.Increment(ref timeoutAttempts) == 1)
                {
                    throw new TimeoutException("Retryable timeout.");
                }

                return Task.CompletedTask;
            },
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)),
            new RetryPolicy
            {
                MaxRetries = 1,
                InitialDelay = TimeSpan.Zero,
                ShouldRetryException = exception => exception is TimeoutException
            });

        var invalidJobId = scheduler.ScheduleJob(
            "Do not retry invalid operation",
            _ =>
            {
                Interlocked.Increment(ref invalidAttempts);
                throw new InvalidOperationException("Not retryable by classifier.");
            },
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)),
            new RetryPolicy
            {
                MaxRetries = 3,
                InitialDelay = TimeSpan.Zero,
                ShouldRetryException = exception => exception is TimeoutException
            });

        var timeoutResult = await scheduler.TriggerJobAsync(timeoutJobId).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        var invalidResult = await scheduler.TriggerJobAsync(invalidJobId).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);

        Output.KeyValue("TimeoutException result", $"{timeoutResult}, attempts={timeoutAttempts}");
        Output.KeyValue("InvalidOperation result", $"{invalidResult}, attempts={invalidAttempts}");

        Require(timeoutResult && Volatile.Read(ref timeoutAttempts) == 2, "TimeoutException should have retried once and succeeded.");
        Require(!invalidResult && Volatile.Read(ref invalidAttempts) == 1, "InvalidOperationException should not have retried.");
    }
}

internal sealed class CancellationScenario : ScenarioBase
{
    public CancellationScenario(ConsoleOutput output) : base(output)
    {
    }

    public override string Key => "cancellation";
    public override string Name => "Cancellation";
    public override string Description => "Shows OperationCanceledException is observed without ordinary retry or final-failure alerting.";

    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var provider = DemoSchedulerFactory.CreateProvider();
        var scheduler = provider.GetRequiredService<global::IScheduler>();
        var attempts = 0;
        var failureEvents = 0;
        scheduler.JobFailed += (_, _) => Interlocked.Increment(ref failureEvents);

        var jobId = scheduler.ScheduleJob(
            "Canceled job",
            token =>
            {
                Interlocked.Increment(ref attempts);
                throw new OperationCanceledException(token);
            },
            new OneTimeTrigger(TimeProvider.System.GetUtcNow().AddHours(1)),
            new RetryPolicy { MaxRetries = 3, InitialDelay = TimeSpan.Zero });

        var result = await scheduler.TriggerJobAsync(jobId).WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
        var job = scheduler.GetScheduledJobs().Single(item => item.Id == jobId);

        Output.KeyValue("Trigger result", result);
        Output.KeyValue("Attempts", attempts);
        Output.KeyValue("Status", job.Status);
        Output.KeyValue("Final failure events", failureEvents);

        Require(!result, "Canceled job should report false.");
        Require(Volatile.Read(ref attempts) == 1, "Canceled job should not retry.");
        Require(job.Status == JobStatus.Canceled, "Canceled job should end in Canceled status.");
        Require(Volatile.Read(ref failureEvents) == 0, "Cancellation should not emit final failure.");
    }
}
