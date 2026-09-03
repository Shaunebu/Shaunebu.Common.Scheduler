namespace Shaunebu.Common.Scheduler.Client.Scenarios;

internal interface ISchedulerScenario
{
    string Key { get; }
    string Name { get; }
    string Description { get; }
    Task RunAsync(CancellationToken cancellationToken);
}

internal sealed record ScenarioResult(
    string Key,
    string Name,
    bool Success,
    TimeSpan Duration,
    string? Message = null,
    Exception? Exception = null)
{
    public static ScenarioResult Pass(string key, string name, TimeSpan duration, string? message = null)
    {
        return new ScenarioResult(key, name, true, duration, message);
    }

    public static ScenarioResult Fail(string key, string name, TimeSpan duration, string message, Exception? exception = null)
    {
        return new ScenarioResult(key, name, false, duration, message, exception);
    }
}
