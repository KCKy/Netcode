using System.CommandLine;
using Microsoft.Extensions.Logging;
using System.CommandLine.Invocation;
using Command = System.CommandLine.Command;

namespace Tester.Stress;

class StressTest : ITestGame
{
    static StressTest() => TestCommon.CreateServerClientHandler(Command, RunServerAsync, RunClientAsync);
    public static void Register(RootCommand root) => root.AddCommand(Command);

    static readonly Option<double> Duration = new("--duration", "The duration of the test in seconds.");
    static readonly Command Command = new("stress", "Tests how GameNewt behaves under stress.")
    {
        Duration
    };

    static async Task RunServerAsync(InvocationContext ctx)
    {
        double duration = ctx.GetOption(Duration);

        (var server, ILogger logger) = TestCommon.ConstructServer<ClientInput, ServerInput, GameState, StressTest>(ctx);

        logger.LogInformation("Starting stress detection test server with duration {Duration}.", duration);

        Task task = server.RunAsync();

        await Task.Delay(TimeSpan.FromSeconds(duration));

        server.Terminate();

        await TestCommon.AwaitServerCancellation(task, logger, ctx);
    }

    static async Task RunClientAsync(InvocationContext ctx)
    {
        (var client, ILogger logger) = TestCommon.ConstructClient<ClientInput, ServerInput, GameState, StressTest>(ctx);

        logger.LogInformation("Starting stress detection test client.");

        await TestCommon.RunUntilCompleteAsync(client, logger, ctx);
    }
}
