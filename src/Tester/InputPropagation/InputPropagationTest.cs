using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;
using Kcky.GameNewt.Client;
using Kcky.GameNewt.Utility;
using Kcky.Useful;
using Tester;
using Tester.InputReceive;
using Command = System.CommandLine.Command;

namespace DistantInputPropagation;

class InputPropagationTest : ITestGame
{
    static InputPropagationTest() => TestCommon.CreateServerClientHandler(Command, RunServerAsync, RunClientAsync);
    public static void Register(RootCommand root) => root.AddCommand(Command);

    static readonly Option<int> Warmup = new("--warmup", "The number of frame the client may miss");
    static readonly Option<int> TestDuration = new("--duration", "The duration of the test in frames.");
    static readonly Option<int> Count = new("--count", "The number of connected players.");
    static readonly Command Command = new("prop", "Tests that distant client inputs are being processed in auth and predict state.")
    {
        Warmup,
        TestDuration,
        Count
    };

    static async Task RunServerAsync(InvocationContext ctx)
    {
        int duration = ctx.GetOption(TestDuration);

        (var server, ILogger logger) = TestCommon.ConstructServer<ClientInput, ServerInput, GameState, InputPropagationTest>(ctx);
        server.OnStateInit += state => state.TotalFrames = duration;

        logger.LogInformation("Starting propagation test server for duration {Duration} ", duration);

        await TestCommon.RunUntilCompleteAsync(server, logger, ctx);
    }

    static async Task RunClientAsync(InvocationContext ctx)
    {
        Client<ClientInput, ServerInput, GameState> client = null!;
        (client, ILogger logger) = TestCommon.ConstructClient<ClientInput, ServerInput, GameState, InputPropagationTest>(ctx, clientProvider: ProvideInput);

        int warmup = ctx.GetOption(Warmup);
        int count = ctx.GetOption(Count);

        object authMutex = new();
        PooledBufferWriter<byte> writer = new();
        GameState lastAuth = new();

        ClientInput ProvideInput()
        {
            return new()
            {
                Value = (int)(client ?? throw new()).PredictFrame + 1
            };
        }

        client.OnNewAuthoritativeState += (frame, state) =>
        {
            lock (authMutex)
            {
                writer.Copy(state, ref lastAuth!);
            }
            
            if (state.Frame != frame)
            {
                logger.LogError("Logical frame number and actual mismatch {A} != {B}", state.Frame, frame);
                ctx.FlagTestFail();
                return;
            }

            if (frame <= warmup)
                return; // Too soon to test

            if (state.ClientIdToReceivedInputs.Count != count)
            {
                logger.LogError("There is unexpected number of players after warmup! {A} != {B}", state.ClientIdToReceivedInputs.Count, count);
                ctx.FlagTestFail();
            }

            foreach ((int id, List<int> inputs)  in state.ClientIdToReceivedInputs)
            {
                if (inputs.Count == 0 || inputs[^1] != frame)
                {
                    logger.LogError("Last input for frame {frame} missing for {Id}.", frame, id);
                    ctx.FlagTestFail();
                    return;
                }

                if (!inputs.SkipWhile(x => x <= warmup).IsConsecutive(out int previous, out int current))
                {
                    logger.LogError("Inputs are not consecutive {Previous} < {Current} for id {Id}.", previous, current, id);
                    ctx.FlagTestFail();
                    return;
                }
            }
        };

        client.OnNewPredictiveState += (frame, state) =>
        {
            if (state.Frame != frame)
            {
                logger.LogError("Logical frame number and actual mismatch for predictive {A} != {B}", state.Frame, frame);
                ctx.FlagTestFail();
                return;
            }

            foreach ((int id, List<int> input) in state.ClientIdToReceivedInputs)
            {
                if (!input.SkipWhile(x => x <= warmup).IsAscendingWithoutGaps(out int previous, out int current))
                {
                    logger.LogError("Inputs are not proper for predictive state {Previous} <= {Current} for id {Id}.", previous, current, id);
                    ctx.FlagTestFail();
                }
            }

            lock (authMutex)
            {
                if (lastAuth.Frame <= warmup)
                    return;

                if (lastAuth.Frame > frame)
                {
                    logger.LogError("Auth is newer than predict {A} < {B}.", lastAuth.Frame, frame);
                    ctx.FlagTestFail();
                    return;
                }

                foreach ((int id, IEnumerable<int> inputs) in lastAuth.ClientIdToReceivedInputs)
                {
                    if (!state.ClientIdToReceivedInputs.TryGetValue(id, out List<int>? predInputs))
                    {
                        logger.LogError("Structure is missing {Id}, although it is in auth.", id);
                        ctx.FlagTestFail();
                        return;
                    }

                    if (!inputs.IsPrefixOf(predInputs))
                    {
                        logger.LogError("Inputs are missing for id {Id}, although they are in auth.", id);
                        ctx.FlagTestFail();
                        return;
                    }
                }
            }
        };

        logger.LogInformation("Starting propagation test client with test warmup {Warmup} and player count {Count}.", warmup,  count);

        await TestCommon.RunUntilCompleteAsync(client, logger, ctx);
    }
}
