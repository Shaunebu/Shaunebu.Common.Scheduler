using Shaunebu.Common.Scheduler.Client.Infrastructure;
using Shaunebu.Common.Scheduler.Client.Scenarios;

namespace Shaunebu.Common.Scheduler.Client;

internal sealed class DemoApplication
{
    private readonly ScenarioRegistry _registry;
    private readonly ConsoleOutput _output;

    public DemoApplication(ScenarioRegistry registry, ConsoleOutput output)
    {
        _registry = registry;
        _output = output;
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            return await RunInteractiveAsync(cancellationToken);
        }

        var command = CommandLineOptions.Parse(args);
        if (!command.IsValid)
        {
            _output.Error(command.ErrorMessage ?? "Invalid command.");
            PrintUsage();
            return 2;
        }

        if (command.ListScenarios)
        {
            PrintScenarioList();
            return 0;
        }

        if (command.ValidateAll || string.Equals(command.ScenarioKey, "all", StringComparison.OrdinalIgnoreCase))
        {
            var results = await RunCompleteShowcaseAsync(cancellationToken);
            return results.All(result => result.Success) ? 0 : 1;
        }

        if (!string.IsNullOrWhiteSpace(command.ScenarioKey))
        {
            var result = await _registry.RunAsync(command.ScenarioKey, cancellationToken);
            _output.Summary(new[] { result });
            return result.Success ? 0 : 1;
        }

        PrintUsage();
        return 2;
    }

    private async Task<int> RunInteractiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            _output.Title("Shaunebu.Common.Scheduler - Feature Demo");
            _output.Line("Select a scenario:");
            _output.Line();

            var scenarios = _registry.Scenarios.ToArray();
            for (var index = 0; index < scenarios.Length; index++)
            {
                _output.Line($"{index + 1,2}. {scenarios[index].Name}");
            }

            var runAllNumber = scenarios.Length + 1;
            _output.Line($"{runAllNumber,2}. Run Complete Showcase");
            _output.Line(" 0. Exit");
            _output.Line();
            _output.Write("Choice: ");

            var input = Console.ReadLine();
            if (string.Equals(input, "0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (int.TryParse(input, out var runAllSelection) && runAllSelection == runAllNumber)
            {
                await RunCompleteShowcaseAsync(cancellationToken);
                _output.Line();
                _output.Line("Press Enter to return to the menu.");
                Console.ReadLine();
                continue;
            }

            if (int.TryParse(input, out var number) && number >= 1 && number <= scenarios.Length)
            {
                var result = await _registry.RunAsync(scenarios[number - 1].Key, cancellationToken);
                _output.Summary(new[] { result });
                _output.Line();
                _output.Line("Press Enter to return to the menu.");
                Console.ReadLine();
                continue;
            }

            if (string.Equals(input, "all", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(input, "run-all", StringComparison.OrdinalIgnoreCase))
            {
                await RunCompleteShowcaseAsync(cancellationToken);
                _output.Line();
                _output.Line("Press Enter to return to the menu.");
                Console.ReadLine();
                continue;
            }

            if (!string.IsNullOrWhiteSpace(input))
            {
                var result = await _registry.RunAsync(input, cancellationToken);
                _output.Summary(new[] { result });
                _output.Line();
                _output.Line("Press Enter to return to the menu.");
                Console.ReadLine();
            }
        }
    }

    private async Task<IReadOnlyCollection<ScenarioResult>> RunCompleteShowcaseAsync(CancellationToken cancellationToken)
    {
        _output.ScenarioHeader("Run Complete Showcase");
        _output.Line($"Running {_registry.Scenarios.Count} scenarios...");

        var results = await _registry.RunAllAsync(cancellationToken);
        _output.Summary(results);
        return results;
    }

    private void PrintScenarioList()
    {
        _output.Title("Available Scenarios");
        foreach (var scenario in _registry.Scenarios)
        {
            _output.Line($"{scenario.Key,-22} {scenario.Name}");
            _output.Line($"  {scenario.Description}");
        }
    }

    private void PrintUsage()
    {
        _output.Title("Usage");
        _output.Line("dotnet run --project Shaunebu.Common.Scheduler.Client -c Release");
        _output.Line("dotnet run --project Shaunebu.Common.Scheduler.Client -c Release -- --list-scenarios");
        _output.Line("dotnet run --project Shaunebu.Common.Scheduler.Client -c Release -- --scenario <key>");
        _output.Line("dotnet run --project Shaunebu.Common.Scheduler.Client -c Release -- --scenario all");
        _output.Line("dotnet run --project Shaunebu.Common.Scheduler.Client -c Release -- --validate-all");
    }
}

internal sealed class CommandLineOptions
{
    public bool ListScenarios { get; private init; }
    public bool ValidateAll { get; private init; }
    public string? ScenarioKey { get; private init; }
    public bool IsValid { get; private init; } = true;
    public string? ErrorMessage { get; private init; }

    public static CommandLineOptions Parse(string[] args)
    {
        var listScenarios = false;
        var validateAll = false;
        string? scenarioKey = null;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--list-scenarios", StringComparison.OrdinalIgnoreCase))
            {
                listScenarios = true;
            }
            else if (string.Equals(arg, "--validate-all", StringComparison.OrdinalIgnoreCase))
            {
                validateAll = true;
            }
            else if (string.Equals(arg, "--scenario", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    return Invalid("--scenario requires a scenario key.");
                }

                scenarioKey = args[++index];
            }
            else
            {
                return Invalid($"Unknown argument: {arg}");
            }
        }

        var selectedModes = new[] { listScenarios, validateAll, scenarioKey != null }.Count(selected => selected);
        if (selectedModes != 1)
        {
            return Invalid("Specify exactly one command mode.");
        }

        return new CommandLineOptions
        {
            ListScenarios = listScenarios,
            ValidateAll = validateAll,
            ScenarioKey = scenarioKey
        };
    }

    private static CommandLineOptions Invalid(string message)
    {
        return new CommandLineOptions
        {
            IsValid = false,
            ErrorMessage = message
        };
    }
}
