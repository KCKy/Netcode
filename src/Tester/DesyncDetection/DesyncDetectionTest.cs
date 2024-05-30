using System.CommandLine;
using Microsoft.Extensions.Logging;
using System.CommandLine.Invocation;
using Command = System.CommandLine.Command;

namespace Tester.DesyncDetection;

class DesyncDetectionTest : ITestGame
{
    static DesyncDetectionTest() => TestCommon.CreateServerClientHandler(Command, RunServerAsync, RunClientAsync);
    public static void Register(RootCommand root) => root.AddCommand(Command);

    static readonly Option<int> Magic = new("--magic", "The magic used to differ non-deterministically from others in game steps.");
    static readonly Option<double> Duration = new("--duration", "The duration of the test in seconds.");
    static readonly Command Command = new("desync", "Tests whether desync detection works.")
    {
        Duration,
        Magic
    };

    static async Task RunServerAsync(InvocationContext ctx)
    {
        double duration = ctx.GetOption(Duration);
        int magic = ctx.GetOption(Magic);
        GameState.NotPartOfState = magic;

        (var server, ILogger logger) = TestCommon.ConstructServer<ClientInput, ServerInput, GameState, DesyncDetectionTest>(ctx);

        logger.LogInformation("Starting desync detection test server with duration {Duration} frames and magic {Magic}.", duration, magic);
        
        Task task = server.RunAsync();

        await Task.Delay(TimeSpan.FromSeconds(duration));

        server.Terminate();

        await TestCommon.AwaitServerCancellation(task, logger, ctx);
    }

    static async Task RunClientAsync(InvocationContext ctx)
    {
        (var client, ILogger logger) = TestCommon.ConstructClient<ClientInput, ServerInput, GameState, DesyncDetectionTest>(ctx);

        int magic = ctx.GetOption(Magic);
        GameState.NotPartOfState = magic;

        object mutex = new();
        int frameCount = 0;

        client.OnNewAuthoritativeState += (_, _) =>
        {
            lock (mutex)
                frameCount++;
        };

        logger.LogInformation("Starting desync detection test client with magic {Magic}.", magic);

        await TestCommon.RunUntilCompleteAsync(client, logger, ctx);

        if (frameCount > 1)
        {
            logger.LogError("Too many frames happened with desync: {Frames}.", frameCount);
            ctx.FlagTestFail();
        }
    }
}
