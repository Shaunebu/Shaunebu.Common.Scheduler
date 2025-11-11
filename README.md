Shaunebu.Common.Scheduler ⏰🔄
=============================

![Platform](https://img.shields.io/badge/Platform-.NET%20Standard%202.0%2B-purple?logo=dotnet) ![License](https://img.shields.io/badge/License-MIT-lightgrey?logo=opensourceinitiative) ![Production Ready](https://img.shields.io/badge/Production-Ready-brightgreen?logo=check-circle) ![Performance](https://img.shields.io/badge/Performance-🚀%20High%20Throughput-ff6b6b) ![Easy](https://img.shields.io/badge/Easy-😊%20Developer%20Friendly-51cf66)

![NuGet Version](https://img.shields.io/nuget/v/Shaunebu.Common.Scheduler?color=blue&label=NuGet) 

![NET Support](https://img.shields.io/badge/.NET%20Standard-%3E%3D2.0-blueviolet) ![NET](https://img.shields.io/badge/.NET%20Core-%3E%3D3.1-blueviolet) ![MAUI](https://img.shields.io/badge/.NET%20MAUI-%3E%3D8.0-512BD4?logo=dotnet) ![Enterprise](https://img.shields.io/badge/Enterprise-🏢%20Grade-0052cc)

![Scheduler](https://img.shields.io/badge/Scheduler-Cron%20%7C%20Intervals%20%7C%20Custom-blue) ![Distributed](https://img.shields.io/badge/Distributed%20Locking-✅-green) ![Retries](https://img.shields.io/badge/Retry%20Policies-Exponential%20%7C%20Custom-orange) ![Monitoring](https://img.shields.io/badge/Monitoring-Metrics%20%7C%20Analytics-success)

[![Support](https://img.shields.io/badge/support-buy%20me%20a%20coffee-FFDD00)](https://buymeacoffee.com/jcz65te)


## Overview ✨

* * *

`Shaunebu.Common.Scheduler` is a **high-performance, enterprise-grade scheduling library** for .NET applications that provides **advanced job scheduling, retry policies, distributed locking, and comprehensive monitoring**. Built for modern cloud-native applications with support for complex scheduling scenarios.


##Feature Comparison 🆚

* * *

| Feature | **Hangfire** | **[Quartz.NET](https://quartz.net/)** | **Shaunebu.Scheduler** | Benefit |
| --- | --- | --- | --- | --- |
| **Setup Complexity** 🏗️ | Medium | High | ✅ **Low** | Faster time to production |
| **Retry Policies** 🔄 | Basic | Basic | ✅ **Advanced** | Exponential backoff, custom strategies |
| **Distributed Locks** 🔒 | ❌ Limited | ✅ With setup | ✅ **Built-in** | Multi-instance support out-of-the-box |
| **Job History** 📊 | ✅ Basic | ❌ Manual | ✅ **Comprehensive** | Built-in analytics and metrics |
| **Plugin System** 🔌 | ❌ | ✅ Limited | ✅ **Extensible** | Custom monitoring and auditing |
| **MAUI Support** 📱 | ❌ | ❌ | ✅ **Native** | Mobile and desktop optimized |
| **Performance** 🚀 | Good | Good | ✅ **Excellent** | Optimized for high throughput |
| **Dependencies** 📦 | Heavy | Moderate | ✅ **Lightweight** | Faster startup, smaller footprint |

* * *

## Installation 📦

```bash
dotnet add package Shaunebu.Common.Scheduler
```

* * *

## Quick Start 🚀

### 1. Basic Setup

```csharp
// In MauiProgram.cs or Startup
builder.Services.AddShaunebuScheduler(options =>
{
    options.MaxConcurrentJobs = 5;
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
    options.ContinueOnFailure = true;
})
.AddConsole()
.AddSchedulerPlugins();
```

### 2. Simple Job Scheduling

```csharp
public class WeatherService
{
    private readonly IScheduler _scheduler;

    public WeatherService(IScheduler scheduler)
    {
        _scheduler = scheduler;
        
        // Schedule weather data refresh every 30 minutes
        _scheduler.ScheduleJob("WeatherRefresh", async (ct) =>
        {
            await RefreshWeatherDataAsync();
        }, 30.EveryMinutes());
    }
}
```

* * *

## Core Features 🎯


### 1. Multiple Trigger Types 🎪

```csharp
// Cron expressions
_scheduler.ScheduleJob("CronJob", DoWork, "0 */2 * * *".Cron());

// Simple intervals
_scheduler.ScheduleJob("IntervalJob", DoWork, 15.EveryMinutes());

// Daily at specific time
_scheduler.ScheduleJob("DailyJob", DoWork, 9.Hours().DailyAt());

// Business days only (skip weekends/holidays)
_scheduler.ScheduleJob("BusinessJob", DoWork, 
    new BusinessDayTrigger(TimeSpan.FromHours(9)));

// One-time execution
_scheduler.ScheduleJob("OneTimeJob", DoWork, 
    DateTimeOffset.Now.AddHours(2).RunOnceAt());
```

### 2. Advanced Retry Policies 🔄

```csharp
_scheduler.ScheduleJob("PaymentProcessing", async (ct) =>
{
    await ProcessPaymentAsync();
}, 5.EveryMinutes(), new RetryPolicy
{
    MaxRetries = 5,
    BackoffStrategy = RetryBackoffStrategy.Exponential,
    InitialDelay = TimeSpan.FromSeconds(1),
    MaxDelaySeconds = 300,
    ShouldRetry = ex => ex is not BusinessException
});
```

### 3. Stateful Jobs 🏢

```csharp
public class ReportGenerationState
{
    public int ReportId { get; set; }
    public string Format { get; set; }
    public int AttemptCount { get; set; }
}

var state = new ReportGenerationState { ReportId = 123, Format = "PDF" };
_scheduler.ScheduleJob("ReportJob", state, async (reportState, ct) =>
{
    reportState.AttemptCount++;
    await GenerateReportAsync(reportState.ReportId, reportState.Format);
}, 1.EveryHours());
```

### 4. Distributed Locking 🔒

```csharp
// Automatic distributed locking for multi-instance deployments
_scheduler.ScheduleJob("ClusterSafeJob", async (ct) =>
{
    // This job will only run on one instance in a cluster
    await PerformClusterSafeOperationAsync();
}, 10.EveryMinutes());
```

### 5. Comprehensive Monitoring 📊

```csharp
// Get job analytics
var analytics = await _scheduler.GetJobAnalyticsAsync(
    jobId, 
    DateTime.Today.AddDays(-7), 
    DateTime.Today);

Console.WriteLine($"Success Rate: {analytics.SuccessRate:P2}");
Console.WriteLine($"Average Duration: {analytics.AverageDuration:mm\\:ss}");
Console.WriteLine($"Total Executions: {analytics.TotalExecutions}");
```

* * *

## Enterprise Features 🏢


### 1. Plugin System 🔌

```csharp
services.AddShaunebuScheduler()
    .AddSchedulerPlugins(options =>
    {
        options.EnableAuditPlugin = true;     // Log all job executions
        options.EnableMetricsPlugin = true;   // Collect performance metrics
        options.CustomPlugins = [typeof(MyCustomPlugin)];
    });

// Custom plugin example
public class SecurityAuditPlugin : ISchedulerPlugin
{
    public Task OnJobExecutingAsync(IScheduledJob job)
    {
        _auditService.LogJobStart(job.Id, job.Name, DateTime.UtcNow);
        return Task.CompletedTask;
    }
}
```

### 2. Alerting System 🚨

```csharp
// Register alert providers
services.AddShaunebuScheduler()
    .WithEmailAlerts(options =>
    {
        options.SmtpServer = "smtp.company.com";
        options.ToAddresses = ["team@company.com"];
    })
    .WithSlackAlerts("https://hooks.slack.com/...");

// Alerts are automatically sent for:
// - Job failures after retries exhausted
// - Unexpected execution errors
// - System health issues
```

### 3. Bulk Operations 📚

```csharp
// Bulk schedule multiple jobs
var jobs = new[]
{
    new JobDefinition { Name = "Job1", Trigger = "0 * * * *".Cron() },
    new JobDefinition { Name = "Job2", Trigger = 30.EveryMinutes() }
};

await _scheduler.BulkScheduleAsync(jobs);

// Bulk management
await _scheduler.BulkPauseAsync(["Job1", "Job2"]);
await _scheduler.BulkResumeAsync(["Job1"]);
await _scheduler.BulkUnscheduleAsync(["Job2"]);
```

### 4. Persistence & Recovery 💾

```csharp
// SQL Server persistence
services.AddShaunebuScheduler()
    .WithSqlPersistence(connectionString);

// Jobs survive application restarts
// Execution history is preserved
// Failed jobs can be analyzed and retried
```

* * *

## Advanced Usage 🛠️


### 1. Complex Workflow Scheduling

```csharp
// Chain jobs: Job2 runs after Job1 completes
var job1 = _scheduler.ScheduleJob("DataExtraction", ExtractData, 9.EveryHours());
var job2 = _scheduler.ScheduleJob("DataProcessing", ProcessData, 
    new ChainedTrigger(job1, _scheduler));

// Conditional scheduling based on events
var eventTrigger = new EventTrigger("DataReady", _eventBus);
_scheduler.ScheduleJob("EventDrivenJob", ProcessReadyData, eventTrigger);
```

### 2. Custom Triggers

```csharp
public class MarketHoursTrigger : IScheduleTrigger
{
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset from)
    {
        // Only run during market hours (9:30 AM - 4:00 PM ET)
        var easternTime = TimeZoneInfo.ConvertTime(from, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
        
        var nextMarketOpen = GetNextMarketOpen(easternTime);
        return new DateTimeOffset(nextMarketOpen, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time").GetUtcOffset(nextMarketOpen));
    }
}

_scheduler.ScheduleJob("MarketDataJob", CollectMarketData, new MarketHoursTrigger());
```

### 3. Health Monitoring Integration

```csharp
public class SchedulerHealthCheck : IHealthCheck
{
    private readonly IScheduler _scheduler;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var jobs = _scheduler.GetScheduledJobs();
        var failedJobs = jobs.Count(j => j.Status == JobStatus.Failed);
        
        return failedJobs > 0 
            ? HealthCheckResult.Degraded($"{failedJobs} jobs in failed state")
            : HealthCheckResult.Healthy("All jobs running normally");
    }
}
```

* * *

## MAUI Integration 📱


### 1. Lifecycle-Aware Scheduling

```csharp
public partial class App : Application
{
    private readonly IScheduler _scheduler;

    public App(IScheduler scheduler)
    {
        _scheduler = scheduler;
        InitializeComponent();
        MainPage = new MainPage();
    }

    protected override async void OnStart()
    {
        await _scheduler.StartAsync();
    }

    protected override async void OnSleep()
    {
        await _scheduler.StopAsync();
    }

    protected override async void OnResume()
    {
        await _scheduler.StartAsync();
    }
}
```

### 2. Background Tasks in MAUI

```csharp
public class LocationTrackingService
{
    private readonly IScheduler _scheduler;

    public LocationTrackingService(IScheduler scheduler)
    {
        _scheduler = scheduler;
        
        // Schedule location updates every 5 minutes
        _scheduler.ScheduleJob("LocationUpdate", async (ct) =>
        {
            var location = await GetCurrentLocationAsync();
            await SaveLocationAsync(location);
        }, 5.EveryMinutes(), new RetryPolicy { MaxRetries = 3 });
    }
}
```

* * *

## Performance Optimization 🚀


### 1. Configuration Tuning

```csharp
services.AddShaunebuScheduler(options =>
{
    options.MaxConcurrentJobs = Environment.ProcessorCount * 2;
    options.JobExecutionTimeout = TimeSpan.FromMinutes(10);
    options.ShutdownTimeout = TimeSpan.FromSeconds(60);
    options.ContinueOnFailure = false; // Stop on failure for critical jobs
});
```

### 2. Memory Management

```csharp
// For long-running applications, configure history retention
services.AddShaunebuScheduler()
    .WithSqlPersistence(connectionString)
    .WithHistoryRetention(TimeSpan.FromDays(30)); // Keep 30 days of history
```

### 3. Monitoring and Metrics

```csharp
// Integrate with application metrics
services.AddShaunebuScheduler()
    .WithApplicationInsights()
    .WithPrometheusMetrics();

// Custom metrics collection
services.AddSingleton<ISchedulerPlugin, CustomMetricsPlugin>();
```

* * *

## Best Practices 📝


### ✅ DO:

```csharp
// Use descriptive job names
_scheduler.ScheduleJob("Customer_Email_Campaign_Processing", ...);

// Implement proper error handling
_scheduler.ScheduleJob("PaymentProcessing", async (ct) =>
{
    try
    {
        await ProcessPaymentsAsync();
    }
    catch (PaymentGatewayException ex)
    {
        _logger.LogError(ex, "Payment gateway unavailable");
        throw; // Let retry policy handle it
    }
}, 5.EveryMinutes());

// Use appropriate retry policies for external dependencies
new RetryPolicy 
{ 
    MaxRetries = 5,
    BackoffStrategy = RetryBackoffStrategy.Exponential,
    ShouldRetry = ex => ex is TransientException
};
```

### ❌ DON'T:

```csharp
// Don't use generic job names
_scheduler.ScheduleJob("Job1", ...); // ❌

// Don't ignore exceptions
_scheduler.ScheduleJob("DataSync", async (ct) =>
{
    await SyncDataAsync(); // ❌ Exceptions are swallowed
});

// Don't schedule too frequently for resource-intensive jobs
_scheduler.ScheduleJob("ReportGeneration", GenerateReport, 10.EverySeconds()); // ❌
```

* * *

## Troubleshooting 🔧


### Common Issues

**Jobs not executing?**
*   Check `MinimumLevel` configuration
    
*   Verify trigger configuration
    
*   Check concurrent job limits
    
**Performance issues?**
*   Reduce batch size for memory-constrained environments
    
*   Increase batch interval for battery-sensitive applications
    
*   Use appropriate retry policies
    
**Memory leaks?**
*   Ensure proper job disposal
    
*   Configure history retention policies
    
*   Monitor job execution durations
    

### Debugging Configuration

```csharp
// Enable detailed logging for troubleshooting
services.AddShaunebuScheduler(options =>
{
    options.LogJobExecutions = true;
})
.AddSchedulerPlugins(options =>
{
    options.EnableAuditPlugin = true; // Detailed execution logging
});

// Check scheduler status
var jobs = _scheduler.GetScheduledJobs();
_logger.LogInformation("Active jobs: {Count}", jobs.Count);
foreach (var job in jobs)
{
    _logger.LogInformation("Job: {Name}, Status: {Status}, Next: {NextRun}", 
        job.Name, job.Status, job.NextRun);
}
```

* * *

## API Reference 📚


### IScheduler Interface

| Method | Description |
| --- | --- |
| `ScheduleJob(name, job, trigger)` | Schedule stateless job |
| `ScheduleJob(name, state, job, trigger)` | Schedule stateful job |
| `ScheduleJob(name, job, trigger, retryPolicy)` | Schedule with retry policy |
| `UnscheduleJob(jobId)` | Remove scheduled job |
| `GetScheduledJobs()` | Get all scheduled jobs |
| `StartAsync()` | Start scheduler |
| `StopAsync()` | Stop scheduler |
| `GetJobAnalyticsAsync(jobId, from, to)` | Get job execution analytics |
| `TriggerJobAsync(jobId)` | Manually trigger job |
| `BulkScheduleAsync(jobs)` | Bulk schedule multiple jobs |
| `BulkUnscheduleAsync(jobIds)` | Bulk remove jobs |

### Trigger Extensions

| Method | Description |
| --- | --- |
| `EverySeconds(seconds)` | Interval trigger in seconds |
| `EveryMinutes(minutes)` | Interval trigger in minutes |
| `EveryHours(hours)` | Interval trigger in hours |
| `Cron(expression)` | Cron expression trigger |
| `DailyAt(time)` | Daily at specific time |
| `RunOnceAt(time)` | One-time execution |
| `RunOnceIn(delay)` | One-time execution after delay |

### Configuration Options

| Option | Default | Description |
| --- | --- | --- |
| `MaxConcurrentJobs` | 5 | Maximum simultaneous job executions |
| `ShutdownTimeout` | 30s | Graceful shutdown timeout |
| `JobExecutionTimeout` | 30m | Maximum job execution time |
| `ContinueOnFailure` | true | Continue scheduling failed jobs |
| `LogJobExecutions` | true | Enable execution logging |

* * *

## Migration Guide 🚚


### From [Quartz.NET](https://quartz.net/)

```csharp
// QUARTZ.NET
var job = JobBuilder.Create<MyJob>()
    .WithIdentity("myJob", "group1")
    .Build();

var trigger = TriggerBuilder.Create()
    .WithIdentity("myTrigger", "group1")
    .WithCronSchedule("0 0/5 * * * ?")
    .Build();

scheduler.ScheduleJob(job, trigger);

// SHAUNEBU SCHEDULER
_scheduler.ScheduleJob("MyJob", async (ct) => 
{
    await MyJob.Execute();
}, "0 0/5 * * * ?".Cron());
```

### From Hangfire

```csharp
// HANGFIRE
RecurringJob.AddOrUpdate<MyService>(
    "my-job", 
    x => x.DoWork(), 
    "*/5 * * * *");

// SHAUNEBU SCHEDULER  
_scheduler.ScheduleJob("MyJob", async (ct) =>
{
    await myService.DoWork();
}, "*/5 * * * *".Cron());
```

* * *

## Examples 🎨

### E-commerce Order Processing

```csharp
public class OrderProcessingService
{
    private readonly IScheduler _scheduler;

    public OrderProcessingService(IScheduler scheduler)
    {
        _scheduler = scheduler;
        
        // Process abandoned carts every hour
        _scheduler.ScheduleJob("AbandonedCartProcessing", async (ct) =>
        {
            var abandonedCarts = await _cartService.GetAbandonedCartsAsync();
            foreach (var cart in abandonedCarts)
            {
                await _emailService.SendReminderAsync(cart);
            }
        }, 1.EveryHours(), new RetryPolicy { MaxRetries = 3 });

        // Clean up old orders daily at 2 AM
        _scheduler.ScheduleJob("OrderCleanup", async (ct) =>
        {
            await _orderService.CleanupOldOrdersAsync();
        }, 2.Hours().DailyAt());
    }
}
```

### IoT Data Collection

```csharp
public class IoTDataService
{
    public IoTDataService(IScheduler scheduler)
    {
        // Collect sensor data every 30 seconds with aggressive retry
        _scheduler.ScheduleJob("SensorDataCollection", async (ct) =>
        {
            var sensorReadings = await _sensorNetwork.ReadAllSensorsAsync();
            await _dataLake.StoreReadingsAsync(sensorReadings);
        }, 30.EverySeconds(), new RetryPolicy
        {
            MaxRetries = 10,
            BackoffStrategy = RetryBackoffStrategy.Linear,
            InitialDelay = TimeSpan.FromSeconds(1)
        });

        // Health check every 5 minutes
        _scheduler.ScheduleJob("DeviceHealthCheck", async (ct) =>
        {
            var healthStatus = await _sensorNetwork.CheckHealthAsync();
            if (!healthStatus.IsHealthy)
            {
                await _alertService.SendAlertAsync(healthStatus);
            }
        }, 5.EveryMinutes());
    }
}
```

### Financial Reporting

```csharp
public class FinancialReportService
{
    public FinancialReportService(IScheduler scheduler)
    {
        // End-of-day reports at 6 PM on business days
        _scheduler.ScheduleJob("EODReporting", async (ct) =>
        {
            var report = await _reportGenerator.GenerateEODReportAsync();
            await _distributionService.DistributeReportAsync(report);
        }, new BusinessDayTrigger(TimeSpan.FromHours(18)));

        // Monthly reconciliation on first business day of month
        _scheduler.ScheduleJob("MonthlyReconciliation", async (ct) =>
        {
            await _accountingService.ReconcileAccountsAsync();
        }, "0 0 1 * *".Cron()); // 1st day of month at midnight
    }
}
```
