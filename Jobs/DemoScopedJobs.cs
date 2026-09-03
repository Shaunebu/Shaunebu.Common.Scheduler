using System.Collections.Concurrent;
using Shaunebu.Common.Scheduler.Abstractions;

namespace Shaunebu.Common.Scheduler.Client.Jobs;

internal sealed class ScopedExecutionRecorder
{
    private readonly ConcurrentQueue<Guid> _dependencyIds = new();
    private readonly ConcurrentQueue<Guid> _disposedDependencyIds = new();
    private int _attempts;

    public IReadOnlyCollection<Guid> DependencyIds => _dependencyIds.ToArray();
    public IReadOnlyCollection<Guid> DisposedDependencyIds => _disposedDependencyIds.ToArray();
    public int Attempts => Volatile.Read(ref _attempts);

    public int RecordAttempt(Guid dependencyId)
    {
        _dependencyIds.Enqueue(dependencyId);
        return Interlocked.Increment(ref _attempts);
    }

    public void RecordDispose(Guid dependencyId)
    {
        _disposedDependencyIds.Enqueue(dependencyId);
    }
}

internal sealed class DemoScopedDependency : IAsyncDisposable
{
    private readonly ScopedExecutionRecorder _recorder;

    public DemoScopedDependency(ScopedExecutionRecorder recorder)
    {
        _recorder = recorder;
    }

    public Guid InstanceId { get; } = Guid.NewGuid();

    public ValueTask DisposeAsync()
    {
        _recorder.RecordDispose(InstanceId);
        return ValueTask.CompletedTask;
    }
}

internal sealed class FlakyScopedJob : IJob
{
    private readonly DemoScopedDependency _dependency;
    private readonly ScopedExecutionRecorder _recorder;

    public FlakyScopedJob(DemoScopedDependency dependency, ScopedExecutionRecorder recorder)
    {
        _dependency = dependency;
        _recorder = recorder;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var attempt = _recorder.RecordAttempt(_dependency.InstanceId);
        if (attempt == 1)
        {
            throw new InvalidOperationException("First scoped attempt fails intentionally.");
        }

        return Task.CompletedTask;
    }
}
