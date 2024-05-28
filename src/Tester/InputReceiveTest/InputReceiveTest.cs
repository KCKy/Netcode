using System.CommandLine;
using Kcky.GameNewt.Client;
using Microsoft.Extensions.Logging;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport.Default;
using System.CommandLine.Invocation;

namespace Tester.InputReceiveTest;

class InputReceiveTest : ITestGame
{
    static readonly Option<int> Warmup = new("--warmup", "The warm-up time in frames.");
    static readonly Option<int> TestDuration = new("--duration", "The duration of the test in frames.");
    static readonly Option<int> GameDuration = new("--game-duration", "The duration of the game in frames. Should be longer than --duration.");
    static readonly Command Command = new("rec", "Tests after a warm-up time whether server receives inputs of the server.")
    {
        Warmup,
        TestDuration,
        GameDuration
    };

    static InputReceiveTest() => Command.SetHandler(Run);
    public static void Register(RootCommand root) => root.AddCommand(Command);

    static async Task Run(InvocationContext ctx)
    {
        if (ctx.IsServer())
        {
            await RunServer(ctx);
        }
        else
        {
            if (!await RunClient(ctx))
                ctx.FlagTestFail();
        }
    }

    static async Task RunServer(InvocationContext ctx)
    {
        ServerParams p = ctx.TryGetServerParams().UnwrapOrThrow();
        ILogger logger = p.Common.TestLogger.CreateLogger<InputReceiveTest>();

        IpServerTransport transport = new(p.Common.Target, p.Common.ComLogger);
        DefaultServerDispatcher dispatcher = new(transport, p.Common.ComLogger);
        Server<ClientInput, ServerInput, GameState> server = new(dispatcher, p.Common.GameLogger)
        {
            SendChecksum = p.Common.Checksum,
            TraceState = p.Common.Trace
        };

        int gameDuration = ctx.ParseResult.GetValueForOption(GameDuration);
        server.OnStateInit += state => state.TotalFrames = gameDuration;

        logger.LogInformation("Starting input receive test server with game duration {Duration} frames.", gameDuration);

        try
        {
            await server.RunAsync();
        }
        catch (OperationCanceledException) { }
    }

    static async Task<bool> RunClient(InvocationContext ctx)
    {
        ClientParams p = ctx.TryGetClientParams().UnwrapOrThrow();
        ILogger logger = p.Common.TestLogger.CreateLogger<InputReceiveTest>();

        IpClientTransport transport = new(p.Common.Target, p.Common.ComLogger);
        DefaultClientDispatcher dispatcher = new(transport, p.Common.ComLogger);

        int warmup = ctx.ParseResult.GetValueForOption(Warmup);
        int testDuration = ctx.ParseResult.GetValueForOption(Warmup);
        int inputCounter = 0;

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

        Client<ClientInput, ServerInput, GameState> client = new(dispatcher, p.Common.GameLogger)
        {
            SamplingWindow = p.SampleWindow,
            TargetDelta = p.TargetDelta,
            TraceState = p.Common.Trace,
            UseChecksum = p.Common.Checksum,
            ClientInputProvider = ProvideInput
        };

        GameState? lastAuthState = null;
        int? id = 0;

        client.OnInitialize += cid => id = cid;
        client.OnNewAuthoritativeState += (long _, GameState state) => lastAuthState = state;

        Task task = client.RunAsync();

        while (!task.IsCompleted)
            client.Update();

        try
        {
            await task;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "The client ended with an exception.");
        }

        if (lastAuthState is null)
        {
            logger.LogError("There was no auth state.");
            return false;
        }

        if (id is not { } validId)
        {
            logger.LogError("An id was not provided.");
            return false;
        }

        if (lastAuthState.Frame != lastAuthState.TotalFrames)
        {
            logger.LogError("The server did not finish.");
        }

        if (!lastAuthState.ClientIdToReceivedInputs.TryGetValue(validId, out List<int>? list) || list is null)
        {
            logger.LogError("Server did not register any server input.");
            return false;
        }

        int expected = 0;
        bool valid = true;
        
        foreach (var input in list)
        {
            valid &= input == expected;
            expected++;
        }

        if (!valid)
        {
            logger.LogError("Some collected inputs are not in sequence: [{A}].", string.Join(',', list));
            return false;
        }

        if (list.Count != testDuration)
        {
            logger.LogError("The input count does not match {Actual} != {Expected}.", list.Count, testDuration);
            return false;
        }

        return true;
    }
}
