using System.CommandLine;
using Microsoft.Extensions.Logging;
using System.CommandLine.Invocation;
using Command = System.CommandLine.Command;
using Kcky.GameNewt.Client;

namespace Tester.InputPrediction;

class InputPredictionTest : ITestGame
{
    static InputPredictionTest() => TestCommon.CreateServerClientHandler(Command, RunServerAsync, RunClientAsync);
    public static void Register(RootCommand root) => root.AddCommand(Command);

    static readonly Option<int> Warmup = new("--warmup", "The number of frames at the beginning to ignore for the test.");
    static readonly Option<double> Duration = new("--duration", "The duration of the test in seconds.");
    static readonly Command Command = new("inpr", "Tests whether input predictions work.")
    {
        Duration,
        Warmup
    };

    static async Task RunServerAsync(InvocationContext ctx)
    {
        double duration = ctx.GetOption(Duration);

        (var server, ILogger logger) = TestCommon.ConstructServer<ClientInput, ServerInput, GameState, InputPredictionTest>(ctx, provider: ProvideInput);

        logger.LogInformation("Starting input prediction test server with duration {Duration}.", duration);

        ServerInput ProvideInput(GameState state)
        {
            return new()
            {
                Value = (int)state.Frame + 1
            };
        }

        Task task = server.RunAsync();

        await Task.Delay(TimeSpan.FromSeconds(duration));

        server.Terminate();

        await TestCommon.AwaitServerCancellation(task, logger, ctx);
    }

    static async Task RunClientAsync(InvocationContext ctx)
    {
        int warmup = ctx.GetOption(Warmup);

        Client<ClientInput, ServerInput, GameState> client = null!;
        (client, ILogger logger) = TestCommon.ConstructClient<ClientInput, ServerInput, GameState, InputPredictionTest>(ctx, clientProvider: ProvideInput, clientPredictor: PredictClientInput, serverPredictor: PredictServerInput);

        ClientInput ProvideInput()
        {
            return new()
            {
                Value = (int)client.PredictFrame + 1
            };
        }

        void PredictClientInput(ref ClientInput input)
        {
            input.Value++;
        }

        void PredictServerInput(ref ServerInput input, GameState state)
        {
            input.Value++;
        }

        long lastPredictiveFrame = long.MinValue;

        client.OnNewPredictiveState += (frame, _) =>
        {
            if (frame <= warmup)
            {
                lastPredictiveFrame = frame;
                return;
            }

            if (lastPredictiveFrame == frame)
            {
                logger.LogError("Detected repeated frame for misprediction fix for frame {Frame}.", frame);
                ctx.FlagTestFail();
            }
            lastPredictiveFrame = frame;
        };

        logger.LogInformation("Starting input prediction test client");

        await TestCommon.RunUntilCompleteAsync(client, logger, ctx);
    }
}
