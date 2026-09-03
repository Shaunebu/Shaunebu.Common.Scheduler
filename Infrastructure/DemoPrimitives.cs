using System.Collections.Concurrent;
using Shaunebu.Common.Scheduler.Abstractions;
using Shaunebu.Common.Scheduler.Events;
using Shaunebu.Common.Scheduler.Models;

namespace Shaunebu.Common.Scheduler.Client.Infrastructure;

internal sealed class RunOnceImmediatelyTrigger : IScheduleTrigger
{
    private int _returned;

    public string Description => "Client demo: run once immediately";

    public DateTimeOffset? GetNextOccurrence(DateTimeOffset from)
    {
        return Interlocked.Exchange(ref _returned, 1) == 0 ? from : null;
    }
}

internal sealed class RecordingSchedulerPlugin : ISchedulerPlugin
{
    private readonly ConcurrentQueue<string> _events = new();

    public IReadOnlyCollection<string> Events => _events.ToArray();

    public Task OnSchedulerStartingAsync(global::IScheduler scheduler)
    {
        _events.Enqueue("scheduler starting");
        return Task.CompletedTask;
    }

    public Task OnSchedulerStoppingAsync(global::IScheduler scheduler)
    {
        _events.Enqueue("scheduler stopping");
        return Task.CompletedTask;
    }

    public Task OnJobExecutingAsync(IScheduledJob job)
    {
        _events.Enqueue($"job executing: {job.Name}");
        return Task.CompletedTask;
    }

    public Task OnJobExecutedAsync(IScheduledJob job, JobExecutionResult? result)
    {
        _events.Enqueue($"job executed: {job.Name} success={result?.Success}");
        return Task.CompletedTask;
    }

    public Task OnJobScheduledAsync(string jobId, string jobName)
    {
        _events.Enqueue($"job scheduled: {jobName} ({jobId})");
        return Task.CompletedTask;
    }

    public Task OnJobUnscheduledAsync(string jobId)
    {
        _events.Enqueue($"job unscheduled: {jobId}");
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingSchedulerPlugin : ISchedulerPlugin
{
    public Task OnSchedulerStartingAsync(global::IScheduler scheduler)
    {
        throw new InvalidOperationException("Intentional client demo plugin failure.");
    }

    public Task OnSchedulerStoppingAsync(global::IScheduler scheduler)
    {
        throw new InvalidOperationException("Intentional client demo plugin failure.");
    }

    public Task OnJobExecutingAsync(IScheduledJob job)
    {
        throw new InvalidOperationException("Intentional client demo plugin failure.");
    }

    public Task OnJobExecutedAsync(IScheduledJob job, JobExecutionResult? result)
    {
        throw new InvalidOperationException("Intentional client demo plugin failure.");
    }

    public Task OnJobScheduledAsync(string jobId, string jobName)
    {
        throw new InvalidOperationException("Intentional client demo plugin failure.");
    }

    public Task OnJobUnscheduledAsync(string jobId)
    {
        throw new InvalidOperationException("Intentional client demo plugin failure.");
    }
}

internal sealed class RecordingAlertProvider : IAlertProvider
{
    private readonly ConcurrentQueue<AlertMessage> _messages = new();

    public IReadOnlyCollection<AlertMessage> Messages => _messages.ToArray();

    public Task SendAlertAsync(AlertMessage message)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingAlertProvider : IAlertProvider
{
    public Task SendAlertAsync(AlertMessage message)
    {
        throw new InvalidOperationException("Intentional client demo alert failure.");
    }
}

internal sealed class SimpleEventBus : IEventBus
{
    private readonly ConcurrentDictionary<string, List<EventHandler<EventReceivedEventArgs>>> _subscriptions = new(StringComparer.Ordinal);

    public void Subscribe(string eventName, EventHandler<EventReceivedEventArgs> handler)
    {
        var handlers = _subscriptions.GetOrAdd(eventName, _ => new List<EventHandler<EventReceivedEventArgs>>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
    }

    public void Unsubscribe(string eventName, EventHandler<EventReceivedEventArgs> handler)
    {
        if (!_subscriptions.TryGetValue(eventName, out var handlers))
        {
            return;
        }

        lock (handlers)
        {
            handlers.Remove(handler);
        }
    }

    public void Publish(string eventName, object data)
    {
        if (!_subscriptions.TryGetValue(eventName, out var handlers))
        {
            return;
        }

        EventHandler<EventReceivedEventArgs>[] snapshot;
        lock (handlers)
        {
            snapshot = handlers.ToArray();
        }

        var args = new EventReceivedEventArgs(data);
        foreach (var handler in snapshot)
        {
            handler(this, args);
        }
    }
}
