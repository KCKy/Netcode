using System.Buffers;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Numerics;
using Kcky.GameNewt;
using Kcky.Useful;
using Kcky.GameNewt.Client;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport;
using Kcky.GameNewt.Transport.Default;
using Microsoft.Extensions.Logging;
using Tester.DesyncDetection;
using Command = System.CommandLine.Command;

namespace Tester;

static class TestCommon
{
    sealed class ClientTransportLossAdaptor : IClientTransport
    {
        readonly object mutex_ = new();
        readonly IClientTransport client_;
        readonly Random random_ = new(42);
        readonly float inputLoss_;
        readonly float outputLoss_;
        bool lastPacketSent_ = false;

        public ClientTransportLossAdaptor(IClientTransport client, float inputLoss, float outputLoss)
        {
            client_ = client;
            client_.OnUnreliableMessage += HandleUnreliableMessage;
            inputLoss_ = inputLoss;
            outputLoss_ = outputLoss;
        }
        
        bool WasPacketReceived()
        {
            lock (mutex_)
                return random_.NextSingle() > inputLoss_;
        }

        bool WasPacketSent()
        {
            lock (mutex_)
            {
                if (lastPacketSent_ && outputLoss_ >= random_.NextSingle())
                {
                    lastPacketSent_ = false;
                    return false;
                }

                lastPacketSent_ = true;
                return true;
            }
        }

        void HandleUnreliableMessage(Memory<byte> message)
        {
            if (!WasPacketReceived())
            {
                ArrayPool<byte>.Shared.Return(message);
                return;
            }
            
            OnUnreliableMessage?.Invoke(message);
        }

        public void SendUnreliable(Memory<byte> message)
        {
            if (!WasPacketSent())
            {
                ArrayPool<byte>.Shared.Return(message);
                return;
            }

            client_.SendUnreliable(message);
        }

        public event ClientMessageEvent? OnUnreliableMessage;
        event ClientMessageEvent? IClientInTransport.OnReliableMessage
        {
            add => client_.OnReliableMessage += value;
            remove => client_.OnReliableMessage -= value;
        }
        public int ReliableMessageHeader => client_.ReliableMessageHeader;
        public void SendReliable(Memory<byte> message) => client_.SendReliable(message);
        public int UnreliableMessageHeader => client_.UnreliableMessageHeader;
        public int UnreliableMessageMaxLength => client_.UnreliableMessageMaxLength;
        public void Terminate() => client_.Terminate();
        public Task RunAsync() => client_.RunAsync();
    }

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

    public static (Client<TClientInput, TServerInput, TGameState> client, ILogger logger) ConstructLossyClient<TClientInput, TServerInput, TGameState, TTest>
    (InvocationContext ctx, float outputLoss, float inputLoss,
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
        ClientTransportLossAdaptor lossTransport = new(transport, inputLoss, outputLoss);
        DefaultClientDispatcher dispatcher = new(lossTransport, p.Common.ComLogger);

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
            if (previous != current && previous + T.One != current)
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
