using System.CommandLine;
using System.CommandLine.Invocation;
using System.Numerics;
using Kcky.GameNewt;
using Kcky.GameNewt.Client;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport.Default;
using Microsoft.Extensions.Logging;
using Tester.DesyncDetection;

namespace Tester;

static class TestCommon
{
    public static (Server<TClientInput, TServerInput, TGameState> server, ILogger logger) ConstructServer<TClientInput, TServerInput, TGameState, TTest>
        (InvocationContext ctx,
            PredictClientInputDelegate<TClientInput>? predictor = null,
            ProvideServerInputDelegate<TServerInput, TGameState>? provider = null)
        where TGameState : class, IGameState<TClientInput, TServerInput>, new()
        where TServerInput : class, new()
        where TClientInput : class, new()
        where TTest : ITestGame
    {
        ServerParams serverParams = ctx.TryGetServerParams().UnwrapOrThrow();
        ILogger logger = serverParams.Common.TestLogger.CreateLogger<TTest>();
        IpServerTransport transport = new(serverParams.Common.Target, serverParams.Common.ComLogger);
        DefaultServerDispatcher dispatcher = new(transport, serverParams.Common.ComLogger);

        Server<TClientInput, TServerInput, TGameState> server = new(dispatcher, serverParams.Common.GameLogger)
        {
            SendChecksum = serverParams.Common.Checksum,
            TraceState = serverParams.Common.Trace,
            ClientInputPredictor = predictor,
            ServerInputProvider = provider
        };

        return (server, logger);
    }

    public static (Client<TClientInput, TServerInput, TGameState> client, ILogger logger) ConstructClient<TClientInput, TServerInput, TGameState, TTest>
        (InvocationContext ctx,
            PredictClientInputDelegate<TClientInput>? clientPredictor = null,
            PredictServerInputDelegate<TServerInput, TGameState>? serverPredictor = null,
            ProvideClientInputDelegate<TClientInput>? clientProvider = null)
        where TGameState : class, IGameState<TClientInput, TServerInput>, new()
        where TServerInput : class, new()
        where TClientInput : class, new()
        where TTest : ITestGame
    {
        ClientParams p = ctx.TryGetClientParams().UnwrapOrThrow();
        ILogger logger = p.Common.TestLogger.CreateLogger<DesyncDetectionTest>();

        IpClientTransport transport = new(p.Common.Target, p.Common.ComLogger);
        DefaultClientDispatcher dispatcher = new(transport, p.Common.ComLogger);

        Client<TClientInput, TServerInput, TGameState> client = new(dispatcher, p.Common.GameLogger)
        {
            SamplingWindow = p.SampleWindow,
            TargetDelta = p.TargetDelta,
            TraceState = p.Common.Trace,
            UseChecksum = p.Common.Checksum,
            ClientInputPredictor = clientPredictor,
            ClientInputProvider = clientProvider,
            ServerInputPredictor = serverPredictor
        };

        return (client, logger);
    }

    public static async Task RunUntilCompleteAsync<TClientInput, TServerInput, TGameState>(Client<TClientInput, TServerInput, TGameState> client, ILogger logger, InvocationContext ctx)
        where TGameState : class, IGameState<TClientInput, TServerInput>, new()
        where TServerInput : class, new()
        where TClientInput : class, new()
    {
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
            return;
        }

        logger.LogError("The client did not end with exception or cancellation.");
        ctx.FlagTestFail();
    }

    public static Task RunUntilCompleteAsync<TClientInput, TServerInput, TGameState>(Server<TClientInput, TServerInput, TGameState> server, ILogger logger, InvocationContext ctx)
        where TGameState : class, IGameState<TClientInput, TServerInput>, new()
        where TServerInput : class, new()
        where TClientInput : class, new()
    {
        return AwaitServerCancellation(server.RunAsync(), logger, ctx);
    }

    public static async Task AwaitServerCancellation(Task task, ILogger logger, InvocationContext ctx)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Server successfully ended.");
            return;
        }
        catch (Exception ex)
        {           
            logger.LogCritical(ex, "Server crashed with an exception.");
            ctx.FlagTestFail();
            return;
        }

        logger.LogError("Server was not cancelled but finished.");
        ctx.FlagTestFail();
    }

    public static void CreateServerClientHandler(Command command, Func<InvocationContext, Task> serverMethod, Func<InvocationContext, Task> clientMethod)
    {
        command.SetHandler(ctx => ctx.IsServer() ? serverMethod(ctx) : clientMethod(ctx));
    }
}

static class EnumerableExtensions
{
    public static bool IsConsecutive<T>(this IEnumerable<T> self, out T badPrevious, out T badCurrent) where T : INumberBase<T>
    {
        badPrevious = T.Zero;
        badCurrent = T.Zero;

        if (!self.Any())
            return true;

        T previous = self.First();

        foreach (T current in self.Skip(1))
        {
            previous++;
            if (previous != current)
            {
                badPrevious = previous;
                badCurrent = current;
                return false;
            }
        }

        return true;
    }

    public static bool IsAscendingWithoutGaps<T>(this IEnumerable<T> self, out T badPrevious, out T badCurrent) where T : INumberBase<T>
    {
        badPrevious = T.Zero;
        badCurrent = T.Zero;

        if (!self.Any())
            return true;

        T previous = self.First();

        foreach (T current in self.Skip(1))
        {
            if (previous != current && ++previous != current)
            {
                badPrevious = previous;
                badCurrent = current;
                return false;
            }

            previous = current;
        }

        return true;
    }

    public static bool IsPrefixOf<T>(this IEnumerable<T> self, IEnumerable<T> other)
        where T : IEqualityOperators<T, T, bool>
    {
        using IEnumerator<T> otherEnum = other.GetEnumerator();
        foreach (T x in self)
        {
            if (!otherEnum.MoveNext())
                return false;

            if (otherEnum.Current != x)
                return false;
        }

        return true;
    }
}

static class RootCommandExtensions
{
    public static void AddGlobalOptions(this RootCommand command, params Option[] options)
    {
        foreach (Option option in options)
            command.AddGlobalOption(option);
    }
}
