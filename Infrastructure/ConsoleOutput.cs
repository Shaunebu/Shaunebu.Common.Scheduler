using Shaunebu.Common.Scheduler.Client.Scenarios;

namespace Shaunebu.Common.Scheduler.Client.Infrastructure;

internal sealed class ConsoleOutput
{
    public void Title(string text)
    {
        Line();
        Line(new string('=', Math.Max(44, text.Length + 2)));
        Line($" {text}");
        Line(new string('=', Math.Max(44, text.Length + 2)));
    }

    public void ScenarioHeader(string text)
    {
        Line();
        Line(new string('-', 54));
        Line($" {text.ToUpperInvariant()}");
        Line(new string('-', 54));
    }

    public void Section(string text)
    {
        Line();
        Line($"{text}:");
    }

    public void KeyValue(string key, object? value)
    {
        Line($"  {key,-28} {value}");
    }

    public void Line(string? text = "")
    {
        Console.WriteLine(text);
    }

    public void Write(string text)
    {
        Console.Write(text);
    }

    public void Error(string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Line(message);
        Console.ForegroundColor = previous;
    }

    public void Summary(IReadOnlyCollection<ScenarioResult> results)
    {
        Title("Summary");
        Line($"{"Scenario",-32} {"Result",-8} Duration");
        Line(new string('-', 54));

        foreach (var result in results)
        {
            var status = result.Success ? "PASS" : "FAIL";
            Line($"{result.Name,-32} {status,-8} {FormatDuration(result.Duration)}");
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                Line($"  {result.Message}");
            }
        }

        Line();
        Line($"Passed:  {results.Count(result => result.Success)}");
        Line($"Failed:  {results.Count(result => !result.Success)}");
        Line($"Total:   {results.Count}");
        Line($"Duration: {FormatDuration(TimeSpan.FromTicks(results.Sum(result => result.Duration.Ticks)))}");
    }

    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds >= 1
            ? $"{duration.TotalSeconds:0.000}s"
            : $"{duration.TotalMilliseconds:0.0}ms";
    }
}
