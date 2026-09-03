using Shaunebu.Common.Scheduler.Client;
using Shaunebu.Common.Scheduler.Client.Infrastructure;

var output = new ConsoleOutput();
var registry = ScenarioRegistry.CreateDefault(output);
var app = new DemoApplication(registry, output);

return await app.RunAsync(args, CancellationToken.None);
