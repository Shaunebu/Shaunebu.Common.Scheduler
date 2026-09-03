using Shaunebu.Common.Scheduler.Client.Infrastructure;

namespace Shaunebu.Common.Scheduler.Client.Scenarios;

internal abstract class ScenarioBase : ISchedulerScenario
{
    protected ScenarioBase(ConsoleOutput output)
    {
        Output = output;
    }

    public abstract string Key { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    protected ConsoleOutput Output { get; }

    public abstract Task RunAsync(CancellationToken cancellationToken);

    protected static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    protected static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetUtcNow().Add(timeout);
        while (TimeProvider.System.GetUtcNow() < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for the scenario condition.");
    }
}
