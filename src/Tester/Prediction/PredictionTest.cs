using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using Kcky.GameNewt.Client;

namespace Tester.PredictionSimple;
class PredictionTest : ITestGame
{
    static PredictionTest() => TestCommon.CreateServerClientHandler(Command, RunServerAsync, RunClientAsync);
    public static void Register(RootCommand root) => root.AddCommand(Command);

    static readonly Option<int> Warmup = new("--warmup", "The warm-up time in frames.");
    static readonly Option<int> TestDuration = new("--duration", "The duration of the test in frames.");
    static readonly Option<double> MaxLag = new("--max-lag", () => 0.001, "Max lag in seconds from collecting input and corresponding draw.");
    static readonly Command Command = new("pred", "Tests that predictions are working correctly and soon after predict taking input.")
    {
        Warmup,
        TestDuration,
        MaxLag
    };

    static Task RunServerAsync(InvocationContext ctx)
    {
        (var server, ILogger logger) = TestCommon.ConstructServer<ClientInput, ServerInput, GameState, PredictionTest>(ctx);

        logger.LogInformation("Starting single prediction test server.");

        return TestCommon.RunUntilCompleteAsync(server, logger, ctx);
    }

    static async Task RunClientAsync(InvocationContext ctx)
    {
        int warmup = ctx.GetOption(Warmup);
        int duration = ctx.GetOption(TestDuration);
        double maxLag = ctx.GetOption(MaxLag);
        int inputCounter = 0;
        List<long> frames = new();
        Dictionary<int, long> magicToInputTime = new();
        
        Client<ClientInput, ServerInput, GameState> client = null!;
        ILogger logger = null!;
        (client, logger) = TestCommon.ConstructClient<ClientInput, ServerInput, GameState, PredictionTest>(ctx, clientProvider: ProvideInput);

        client.OnNewPredictiveState += HandleNewPredict;

        void HandleNewPredict(long frame, GameState state)
        {
            if (state.Frame != frame)
            {
                logger.LogError("Logical frame number and actual mismatch {A} != {B}", state.Frame, frame);
                throw new InvalidOperationException();
            }

            if (magicToInputTime.Remove(state.MyNumber, out long value))
            {
                double seconds = Stopwatch.GetElapsedTime(value).TotalSeconds;
                if (seconds > maxLag)
                {
                    logger.LogError("Bigger lag detected {A} > {B}", seconds, maxLag);
                    throw new InvalidOperationException();
                }
            }

            frames.Add(frame);
        }

        ClientInput ProvideInput()
        {
            if (warmup > 0)
            {
                warmup--;
                return new();
            }

            if (inputCounter >= duration)
            {
                logger.LogInformation("Test finished. Terminating.");
                client.Terminate();
                return new();
            }

            magicToInputTime.Add(inputCounter, Stopwatch.GetTimestamp());

            return new()
            {
                MyNumber = inputCounter++
            };
        }

        logger.LogInformation("Starting single prediction test client with test duration {Duration} frames and warmup {Warmup}.", duration, warmup);

        await TestCommon.RunUntilCompleteAsync(client, logger, ctx);

        if (magicToInputTime.Count > 1)
        {
            logger.LogError("More then 1 unprocessed input.");
            ctx.FlagTestFail();
        }

        if (frames.IsConsecutive(out long previous, out long current))
        {
            logger.LogError("Frames are not consecutive {previous} < {current}.", previous, current);
            ctx.FlagTestFail();
        }
    }
}
