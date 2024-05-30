using System.CommandLine;
using Microsoft.Extensions.Logging;
using System.CommandLine.Invocation;

namespace Tester.Kicking;

class KickTest : ITestGame
{
    static KickTest() => TestCommon.CreateServerClientHandler(Command, RunServerAsync, RunClientAsync);
    public static void Register(RootCommand root) => root.AddCommand(Command);

    static readonly Option<int> Duration = new("--duration", "The duration of the test in frames.");
    static readonly Command Command = new("kick", "Tests whether kicking clients works.")
    {
        Duration
    };

    static Task RunServerAsync(InvocationContext ctx)
    {
        int gameDuration = ctx.GetOption(Duration);

        (var server, ILogger logger) = TestCommon.ConstructServer<ClientInput, ServerInput, GameState, KickTest>(ctx);

        server.OnStateInit += state => state.TotalFrames = gameDuration;

        logger.LogInformation("Starting kicking test server with duration {Duration} frames.", gameDuration);

        return TestCommon.RunUntilCompleteAsync(server, logger, ctx);
    }

    static Task RunClientAsync(InvocationContext ctx)
    {
        (var client, ILogger logger) = TestCommon.ConstructClient<ClientInput, ServerInput, GameState, KickTest>(ctx);
        
        logger.LogInformation("Starting kicking test client.");

        return TestCommon.RunUntilCompleteAsync(client, logger, ctx);
    }
}
