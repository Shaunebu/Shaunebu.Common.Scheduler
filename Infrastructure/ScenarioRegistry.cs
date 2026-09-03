using System.Diagnostics;
using Shaunebu.Common.Scheduler.Client.Scenarios;

namespace Shaunebu.Common.Scheduler.Client.Infrastructure;

internal sealed class ScenarioRegistry
{
    private readonly IReadOnlyList<ISchedulerScenario> _scenarios;
    private readonly Dictionary<string, ISchedulerScenario> _byKey;
    private readonly ConsoleOutput _output;

    private ScenarioRegistry(IEnumerable<ISchedulerScenario> scenarios, ConsoleOutput output)
    {
        _scenarios = scenarios.ToArray();
        _byKey = _scenarios.ToDictionary(scenario => scenario.Key, StringComparer.OrdinalIgnoreCase);
        _output = output;
    }

    public IReadOnlyList<ISchedulerScenario> Scenarios => _scenarios;

    public static ScenarioRegistry CreateDefault(ConsoleOutput output)
    {
        ISchedulerScenario[] scenarios =
        {
            new BasicSchedulingScenario(output),
            new OneTimeScenario(output),
            new IntervalScenario(output),
            new CalendarTriggerScenario(output),
            new StableIdsScenario(output),
            new StatefulJobsScenario(output),
            new ManualTriggeringScenario(output),
            new RetryPoliciesScenario(output),
            new RetryFilteringScenario(output),
            new CancellationScenario(output),
            new ConcurrencyScenario(output),
            new SameJobOverlapScenario(output),
            new ScopedJobsScenario(output),
            new DependencyInjectionScenario(output),
            new GenericHostScenario(output),
            new LifecycleTimeProviderLoggingScenario(output),
            new PluginsMetricsScenario(output),
            new ExecutionHistoryAlertsScenario(output),
            new GuardrailsInspectionScenario(output),
            new SchedulingSemanticsScenario(output),
            new KnownLimitationsScenario(output)
        };

        return new ScenarioRegistry(scenarios, output);
    }

    public async Task<ScenarioResult> RunAsync(string key, CancellationToken cancellationToken)
    {
        if (!_byKey.TryGetValue(key, out var scenario))
        {
            return ScenarioResult.Fail(key, key, TimeSpan.Zero, $"Unknown scenario key '{key}'. Use --list-scenarios.");
        }

        return await RunScenarioAsync(scenario, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ScenarioResult>> RunAllAsync(CancellationToken cancellationToken)
    {
        var results = new List<ScenarioResult>();
        foreach (var scenario in _scenarios)
        {
            results.Add(await RunScenarioAsync(scenario, cancellationToken));
        }

        return results;
    }

    private async Task<ScenarioResult> RunScenarioAsync(ISchedulerScenario scenario, CancellationToken cancellationToken)
    {
        _output.ScenarioHeader(scenario.Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await scenario.RunAsync(cancellationToken);
            stopwatch.Stop();
            _output.Line();
            _output.Line("PASS");
            return ScenarioResult.Pass(scenario.Key, scenario.Name, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _output.Error($"FAIL: {ex.GetType().Name} - {ex.Message}");
            return ScenarioResult.Fail(scenario.Key, scenario.Name, stopwatch.Elapsed, ex.Message, ex);
        }
    }
}
