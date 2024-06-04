using System.CommandLine;
using Microsoft.Extensions.Logging;
using System.CommandLine.Invocation;

namespace Tester.InputReceive;

class InputReceiveTest : ITestGame
{
    static InputReceiveTest() => TestCommon.CreateServerClientHandler(Command, RunServerAsync, RunClientAsync);
    public static void Register(RootCommand root) => root.AddCommand(Command);

    static readonly Option<int> Warmup = new("--warmup", "The warm-up time in frames.");
    static readonly Option<int> TestDuration = new("--duration", "The duration of the test in frames.");
    static readonly Option<int> GameDuration = new("--game-duration", "The duration of the game in frames. Should be longer than --duration.");
    static readonly Option<float> OutputLoss = new("--output-loss", "The chance for the client to lose a packet which is being received.");
    static readonly Option<float> InputLoss = new("--input-loss", "The chance for the client to lose next sent packet after the previous packet has been sent successfully.");
    static readonly Command Command = new("rec", "Tests after a warm-up time whether client inputs are being received in time.")
    {
        Warmup,
        TestDuration,
        GameDuration,
        InputLoss,
        OutputLoss
    };
    
    static async Task RunServerAsync(InvocationContext ctx)
    {
        (var server, ILogger logger) = TestCommon.ConstructServer<ClientInput, ServerInput, GameState, InputReceiveTest>(ctx);

        int gameDuration = ctx.GetOption(GameDuration);
        server.OnStateInit += state => state.TotalFrames = gameDuration;

        logger.LogInformation("Starting input receive test server with game duration {Duration} frames.", gameDuration);

        await TestCommon.RunUntilCompleteAsync(server, logger, ctx);
    }

    static async Task RunClientAsync(InvocationContext ctx)
    {
        int warmup = ctx.GetOption(Warmup);
        int testDuration = ctx.GetOption(TestDuration);
        float inputLoss = ctx.GetOption(InputLoss);
        float outputLoss = ctx.GetOption(OutputLoss);
        
        int inputCounter = 0;
        object mutex = new();
        GameState? lastAuthState = null;
        int? id = null;

        (var client, ILogger logger) = TestCommon.ConstructLossyClient<ClientInput, ServerInput, GameState, InputReceiveTest>(ctx, outputLoss, inputLoss, clientProvider: ProvideInput);

        ClientInput ProvideInput()
        {
            if (warmup > 0)
            {
                warmup--;
                return new();
            }

            if (inputCounter >= testDuration)
                return new();

            return new()
            {
                Value = inputCounter++
            };
        }

        client.OnInitialize += cid => id = cid;
        client.OnNewAuthoritativeState += (frame, state) =>
        {
            lock (mutex)
                lastAuthState = state;

            if (state.Frame != frame)
            {
                logger.LogError("Logical frame number and actual mismatch {A} != {B}", state.Frame, frame);
                ctx.FlagTestFail();
            }
        };

        logger.LogInformation("Starting input receive test client with test duration {Duration} frames, warmup {Warmup}, input loss {InputLoss} and output loss {OutputLoss}.", testDuration, warmup, inputLoss, outputLoss);

        await TestCommon.RunUntilCompleteAsync(client, logger, ctx);

        if (lastAuthState is null)
        {
            logger.LogError("There was no auth state.");
            ctx.FlagTestFail();
            return;
        }

        if (id is not { } validId)
        {
            logger.LogError("An id was not provided.");
            ctx.FlagTestFail();
            return;
        }

        if (!lastAuthState.ClientIdToReceivedInputs.TryGetValue(validId, out List<int>? list))
        {
            logger.LogError("Server did not register any client input.");
            ctx.FlagTestFail();
            return;
        }

        if (list.Count != testDuration)
        {
            logger.LogError("The input count does not match {Actual} != {Expected}.", list.Count, testDuration);
            ctx.FlagTestFail();
            return;
        }

        int previous = 0;
        int current = 0;
        if (list[0] != 0 || !list.IsConsecutive(out previous, out current))
        {
            logger.LogError("Collected inputs are not in sequence: {A} < {B}", previous, current);
            ctx.FlagTestFail();
            return;
        }
    }
}
