# ⏰ Shaunebu.Common.Scheduler

![Platform](https://img.shields.io/badge/Platform-.NET%20Standard%202.0%2B-purple?logo=dotnet) ![License](https://img.shields.io/badge/License-MIT-lightgrey?logo=opensourceinitiative) ![Production Ready](https://img.shields.io/badge/Production-Ready-brightgreen?logo=check-circle) ![Performance](https://img.shields.io/badge/Performance-🚀%20High%20Throughput-ff6b6b) ![Easy](https://img.shields.io/badge/Easy-😊%20Developer%20Friendly-51cf66)

![NuGet Version](https://img.shields.io/nuget/v/Shaunebu.Common.Scheduler?color=blue&label=NuGet) 

![NET Support](https://img.shields.io/badge/.NET%20Standard-%3E%3D2.0-blueviolet) ![NET](https://img.shields.io/badge/.NET%20Core-%3E%3D3.1-blueviolet) ![MAUI](https://img.shields.io/badge/.NET%20MAUI-%3E%3D8.0-512BD4?logo=dotnet) ![Enterprise](https://img.shields.io/badge/Enterprise-🏢%20Grade-0052cc)

![Scheduler](https://img.shields.io/badge/Scheduler-Cron%20%7C%20Intervals%20%7C%20Custom-blue) ![Distributed](https://img.shields.io/badge/Distributed%20Locking-✅-green) ![Retries](https://img.shields.io/badge/Retry%20Policies-Exponential%20%7C%20Custom-orange) ![Monitoring](https://img.shields.io/badge/Monitoring-Metrics%20%7C%20Analytics-success)

[![Support](https://img.shields.io/badge/support-buy%20me%20a%20coffee-FFDD00)](https://buymeacoffee.com/jcz65te)

`Shaunebu.Common.Scheduler` is an in-process scheduler for .NET applications. It supports delegate jobs, DI-resolved typed jobs, one-time and recurring triggers, retry policies, Microsoft.Extensions.DependencyInjection integration, optional Generic Host lifecycle integration, Microsoft.Extensions.Logging, and lightweight plugin callbacks.

> ⚠️ **Important:** This package is designed as a single-process scheduler runtime. It does not provide durable executable-job recovery, cluster coordination, leader election, exactly-once execution across replicas, or built-in SQL/Redis/Prometheus/Slack/Application Insights providers.

## ✨ Key Features

- ⏱️ In-process scheduling for `net9.0` and `net10.0`.
- 📅 One-time, interval, daily, business-day, cron, event, and chained triggers.
- 🆔 Generated job IDs for ordinary `IScheduler` calls.
- 🔖 Caller-supplied stable job IDs through `IStableJobScheduler`.
- 🔧 Delegate jobs and DI-resolved typed jobs through `IJob` and `IScopedJobScheduler`.
- 🔁 Retry policies with fixed, linear, and exponential backoff.
- 🧪 Optional retry exception classification with `RetryPolicy.ShouldRetryException`.
- 🔄 Manual lifecycle through `StartAsync` and `StopAsync`.
- 🏠 Optional Generic Host lifecycle integration through `AddShaunebuSchedulerHostedService()`.
- 🕐 Scheduler time control through `SchedulerOptions.TimeProvider`.
- 📊 Microsoft.Extensions.Logging support and opt-in audit/metrics plugins.
- 🛡️ Optional `MaxScheduledJobs` and `MaxConcurrentJobs` guardrails.

## 📦 Installation

```bash
dotnet add package Shaunebu.Common.Scheduler
```

## 🚀 Quick Start

```csharp
using Shaunebu.Common.Scheduler.Abstractions;
using Shaunebu.Common.Scheduler.Extensions;
using Shaunebu.Common.Scheduler.Triggers;

builder.Services.AddShaunebuScheduler(options =>
{
    options.MaxConcurrentJobs = 4;
    options.MaxScheduledJobs = 1_000;
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

var scheduler = app.Services.GetRequiredService<IScheduler>();
scheduler.ScheduleJob(
    "Heartbeat",
    cancellationToken =>
    {
        Console.WriteLine("Heartbeat");
        return Task.CompletedTask;
    },
    new IntervalTrigger(TimeSpan.FromMinutes(5)));

await scheduler.StartAsync();
await app.RunAsync();
await scheduler.StopAsync();
```

`AddShaunebuScheduler(...)` registers the scheduler and related services, but it does not start the scheduler. Start and stop it yourself, or add the hosted-service integration shown below.

## 🧩 Dependency Injection

```csharp
using Shaunebu.Common.Scheduler.Abstractions;
using Shaunebu.Common.Scheduler.Extensions;
using Shaunebu.Common.Scheduler.Models;

services.AddShaunebuScheduler(options =>
{
    options.MaxConcurrentJobs = 5;
    options.MaxScheduledJobs = null;
    options.TimeProvider = TimeProvider.System;
    options.LogJobExecutions = true;
});

services.AddSchedulerPlugins(options =>
{
    options.EnableAuditPlugin = true;
    options.EnableMetricsPlugin = true;
});
```

Under the standard DI registration, `SchedulerService`, `IScheduler`, and `IStableJobScheduler` resolve to the same singleton scheduler runtime. `IScopedJobScheduler` is a facade over that runtime for typed jobs.

Default infrastructure is in-memory and process-local. The package also preserves compatibility extension methods such as `WithSqlPersistence(...)`, `WithRedisDistributedLocking(...)`, and `WithEmailAlerts(...)`, but those methods do not currently register concrete SQL, Redis, or email providers.

## 🔄 Manual Lifecycle

```csharp
using Microsoft.Extensions.Logging.Abstractions;
using Shaunebu.Common.Scheduler.Models;
using Shaunebu.Common.Scheduler.Providers;
using Shaunebu.Common.Scheduler.Services;

using var scheduler = new SchedulerService(
    new SchedulerOptions(),
    NullLogger<SchedulerService>.Instance,
    new DefaultJobFactory(NullLoggerFactory.Instance));

await scheduler.StartAsync();
await scheduler.StopAsync();
```

Call `StopAsync` during application shutdown so active executions can observe cancellation and the scheduler can wait up to `SchedulerOptions.ShutdownTimeout`. Dispose the scheduler after stopping when you create it manually.

## 🏠 Generic Host Integration

```csharp
using Shaunebu.Common.Scheduler.Extensions;

builder.Services.AddShaunebuScheduler(options =>
{
    options.MaxConcurrentJobs = 4;
});

builder.Services.AddShaunebuSchedulerHostedService();
```

`AddShaunebuSchedulerHostedService()` is opt-in. It starts the existing scheduler singleton when the host starts and stops it when the host stops. Use either manual lifecycle or hosted lifecycle as your primary application pattern.

## 📅 Scheduling Jobs

```csharp
using Shaunebu.Common.Scheduler.Triggers;

var jobId = scheduler.ScheduleJob(
    "Refresh cache",
    async cancellationToken => await RefreshCacheAsync(cancellationToken),
    new IntervalTrigger(TimeSpan.FromMinutes(10)));

var state = new ReportState(42);
var statefulJobId = scheduler.ScheduleJob(
    "Build report",
    state,
    async (reportState, cancellationToken) =>
    {
        await BuildReportAsync(reportState!.ReportId, cancellationToken);
    },
    new OneTimeTrigger(DateTimeOffset.UtcNow.AddHours(1)));
```

The returned job ID is the scheduler identity. The job name is display metadata and is not required to be unique.

## 🆔 Stable Job IDs

```csharp
using Shaunebu.Common.Scheduler.Abstractions;
using Shaunebu.Common.Scheduler.Triggers;

var stableScheduler = serviceProvider.GetRequiredService<IStableJobScheduler>();

stableScheduler.ScheduleJob(
    "nightly-cleanup",
    "Nightly cleanup",
    async cancellationToken => await CleanupAsync(cancellationToken),
    new CronTrigger("0 2 * * *", TimeZoneInfo.Utc));
```

Ordinary `IScheduler.ScheduleJob(...)` calls generate IDs in the existing `{jobName}_{guid:N}` shape. Use `IStableJobScheduler` when the caller needs a known ID for later `UnscheduleJob`, `IsJobScheduled`, `GetScheduledJobs`, or `TriggerJobAsync` calls.

Stable job IDs must not be null, empty, or whitespace. Duplicate stable IDs are rejected with `InvalidOperationException`; existing jobs are not replaced.

## 🔧 Scoped / Typed Jobs

```csharp
using Shaunebu.Common.Scheduler.Abstractions;
using Shaunebu.Common.Scheduler.Triggers;

services.AddScoped<InvoiceDbContext>();
services.AddTransient<SendInvoiceJob>();

var scopedScheduler = serviceProvider.GetRequiredService<IScopedJobScheduler>();
scopedScheduler.ScheduleJob<SendInvoiceJob>(
    "Send invoices",
    new IntervalTrigger(TimeSpan.FromHours(1)));

public sealed class SendInvoiceJob : IJob
{
    private readonly InvoiceDbContext _dbContext;

    public SendInvoiceJob(InvoiceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SendPendingInvoicesAsync(cancellationToken);
    }
}
```

Each typed job execution attempt creates a fresh async DI scope, resolves the job type from that scope, executes it, and disposes the scope. Retry attempts get fresh scopes too. This is useful for scoped dependencies, but it is still in-process execution and does not imply exactly-once delivery.

## ⏱️ Triggers

| Trigger | Purpose |
| --- | --- |
| `OneTimeTrigger` | Executes once at a configured instant. |
| `IntervalTrigger` | Recurs on a fixed interval. |
| `DailyTrigger` | Runs daily at a configured time of day. |
| `BusinessDayTrigger` | Runs on configured business days and skips holidays supplied by an `IHolidayProvider`. |
| `CronTrigger` | Uses NCrontab cron parsing for recurring schedules. |
| `EventTrigger` | Becomes due after an event bus notification. |
| `ChainedTrigger` | Becomes due after another scheduled job completes. |

```csharp
new OneTimeTrigger(DateTimeOffset.UtcNow.AddMinutes(30));
new IntervalTrigger(TimeSpan.FromMinutes(5));
new DailyTrigger(new TimeSpan(9, 0, 0), TimeZoneInfo.Utc);
new BusinessDayTrigger(new TimeSpan(9, 0, 0), TimeZoneInfo.Utc);
new CronTrigger("*/15 * * * *", TimeZoneInfo.Utc);
new EventTrigger("data-ready", eventBus);
new ChainedTrigger(previousJobId, scheduler);
```

Convenience extensions are also available:

```csharp
5.EveryMinutes();
"*/15 * * * *".Cron(TimeZoneInfo.Utc);
new TimeSpan(9, 0, 0).DailyAt(TimeZoneInfo.Utc);
DateTimeOffset.UtcNow.AddHours(1).RunOnceAt();
TimeSpan.FromMinutes(10).RunOnceIn();
```

Cron expressions are parsed by NCrontab. Five-field expressions omit seconds; six-field expressions include seconds. Invalid expressions throw `ArgumentException`. Quartz-only tokens such as `?`, `L`, `W`, and `#` are not supported by NCrontab cron parsing.

Daily, business-day, cron, and time-zone-aware triggers use an explicit `TimeZoneInfo` when supplied and fall back to `TimeZoneInfo.Local` when omitted. Invalid local times during spring-forward DST transitions are skipped to the next valid recurrence. Ambiguous fall-back local times use the earlier occurrence/offset.

## 🔁 Retry Policies

```csharp
using Shaunebu.Common.Scheduler.Enums;
using Shaunebu.Common.Scheduler.Models;

var retryPolicy = new RetryPolicy
{
    MaxRetries = 3,
    InitialDelay = TimeSpan.FromSeconds(1),
    BackoffStrategy = RetryBackoffStrategy.Exponential,
    MaxDelaySeconds = 300,
    ShouldRetryException = exception => exception is TimeoutException
};

scheduler.ScheduleJob(
    "Call external service",
    async cancellationToken => await CallExternalServiceAsync(cancellationToken),
    new IntervalTrigger(TimeSpan.FromMinutes(5)),
    retryPolicy);
```

| Setting | Default | Behavior |
| --- | ---: | --- |
| `MaxRetries` | `3` | Retries after the initial attempt. |
| Initial attempt | `1` | Always attempted once when the job executes. |
| Maximum total attempts | `4` | Initial attempt plus up to three retries when `MaxRetries = 3`. |
| `BackoffStrategy` | `Exponential` | Fixed, linear, and exponential strategies are supported. |

`MaxRetries = 3` means the initial attempt plus up to three retries. Fixed, linear, and exponential backoff are supported. When `ShouldRetryException` is null, ordinary exceptions are retryable. When it returns false, retry stops immediately. Cancellation is treated as cancellation and is not retried.

## 🕐 TimeProvider / Testing

```csharp
services.AddShaunebuScheduler(options =>
{
    options.TimeProvider = TimeProvider.System;
});
```

The configured `TimeProvider` is used for scheduler polling waits, retry waits, shutdown timeout waits, scheduler-owned timestamps, and default in-memory repository metadata. `TimeProvider.System` is the default.

Tests can supply their own `TimeProvider` implementation through `SchedulerOptions.TimeProvider`; this package does not ship a public manual-time test helper.

## 📊 Plugins / Observability

```csharp
services.AddSchedulerPlugins(options =>
{
    options.EnableAuditPlugin = true;
    options.EnableMetricsPlugin = true;
    options.CustomPlugins.Add(typeof(MySchedulerPlugin));
});
```

Plugins implement `ISchedulerPlugin` and receive scheduler starting/stopping, job scheduled/unscheduled, job executing, and job executed callbacks. Plugin failures are logged and isolated from scheduler execution.

`MetricsPlugin` keeps in-memory snapshots for total executions, successful executions, failed executions, total duration, max duration, and last execution per job. It does not export Prometheus or OpenTelemetry metrics.

Logging uses Microsoft.Extensions.Logging and works with any compatible `ILogger` provider configured by the host. `SchedulerOptions.LogJobExecutions = false` suppresses routine job start/success logs while keeping retry, failure, cancellation, lifecycle, and infrastructure diagnostics. Logging `EventId` values are internal implementation details, not a documented public contract.

`IAlertProvider` is called on final job failure after retry exhaustion. The default DI registration uses `ConsoleAlertProvider`; manual construction without an alert provider uses `NullAlertProvider`. Alert provider failures are logged and isolated.

## 🛡️ Operational Guardrails

| Option | Default | Purpose |
| --- | ---: | --- |
| `MaxConcurrentJobs` | `5` | Maximum simultaneous executions across different jobs. |
| `MaxScheduledJobs` | Unlimited | Optional maximum number of registered jobs. |
| `ShutdownTimeout` | 30 seconds | How long shutdown waits for active executions after cancellation is requested. |
| `JobExecutionTimeout` | 30 minutes | Validated option; currently not enforced as a per-job timeout by the scheduler runtime. |
| `ContinueOnFailure` | `true` | Failed recurring jobs remain scheduled after final failure. |
| `LogJobExecutions` | `true` | Emits routine job start and successful completion logs. |
| `TimeProvider` | `TimeProvider.System` | Scheduler clock for polling, waits, timestamps, and default repository metadata. |

`MaxScheduledJobs` defaults to `null`, preserving unlimited scheduling. When configured, scheduling beyond the limit throws `InvalidOperationException`; jobs are not evicted. Negative timeouts and non-positive configured limits are rejected during scheduler construction.

## ⚙️ Scheduling Semantics

| Scenario | Behavior |
| --- | --- |
| Future one-time job | Runs once when due. |
| Exact-now one-time job | Eligible on the next scheduler evaluation. |
| Past one-time job | Does not auto-run; remains registered with `NextRun = null`. |
| Completed or failed one-time job | Remains visible with `NextRun = null` until explicitly unscheduled. |
| Missed recurring occurrence | Skipped; no catch-up queue or burst of historical runs. |
| Same-job overlap | Prevented. |
| Different jobs | May run concurrently, subject to `MaxConcurrentJobs`. |
| Skipped overlapping occurrence | Not queued. |
| Manual trigger | Uses the same execution pipeline as scheduled execution. |
| Future recurring occurrence after manual trigger | Not consumed or advanced by the manual trigger. |

`TriggerJobAsync(jobId)` uses the same execution pipeline as scheduled execution, including retry, history, plugin callbacks, alerts, cancellation, and same-job overlap checks.

## 🚀 Performance Notes

Batch 8 benchmark baselines were collected locally on Windows 11 with .NET SDK `10.0.302` and BenchmarkDotNet `0.15.8`.

| Scheduled jobs | Due distribution | Scan mean |
| ---: | ---: | ---: |
| 10 | 0-50% | 48.11-51.98 ns |
| 100 | 0-50% | 170.20-172.27 ns |
| 1,000 | 0-50% | 1.381-1.391 us |
| 10,000 | 0-50% | 30.491-34.035 us |

Additional measured baselines:

| Operation | Mean |
| --- | ---: |
| In-memory repository snapshot at 10,000 jobs | 58.588 us, 160.216 KB allocated |
| History query at 10,000 records | 192.846 us, 220.624 KB allocated |
| Schedule generated ID | 5.389 us |
| Schedule stable ID | 4.604 us |
| Unschedule job | 695.4 ns |
| Concurrent manual dispatch of 1,000 no-op jobs | 2.326 ms, 1,626.42 KB allocated |

These are local baselines, not service-level guarantees. The scheduler still uses a one-second polling architecture and an O(n) scan of scheduled jobs.

## ⚠️ Current Limitations

- In-process scheduler only; no cluster-safe scheduling or leader election.
- No exactly-once execution across replicas.
- In-memory defaults do not survive process restart.
- Repository abstractions store job definitions/history, but the package does not reconstruct executable delegates after restart.
- No durable executable-job recovery, trigger serialization contract, or crash-safe replay.
- No missed-run replay or catch-up queue.
- No public overlap policy beyond current same-job non-overlap behavior.
- Completed one-shot jobs are retained until unscheduled.
- No built-in SQL, Redis, Slack, Prometheus, OpenTelemetry, or Application Insights provider.
- Polling architecture remains in place for 1.1.0.

## 📦 Package / Framework Support

| Item | Status |
| --- | --- |
| Target frameworks | `net9.0`, `net10.0` |
| Current package version | `1.0.0` until the release/versioning task changes it |
| XML documentation | Enabled |
| Symbol package | Enabled as `.snupkg` |
| Package README | Included |
| Package icon | Not fabricated by this repository |
| License metadata | Not fabricated by this repository |
| Repository URL / Source Link | Not added for this private package |

