using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Core.Extensions;
using Core.Transport;

namespace DefaultTransport.TcpTransport;

public enum ServerFinishReason
{
    Unknown = 0,
    Terminated,
    Fault,
}

public class TcpServerTransport<TIn, TOut> : IServerTransport<TIn, TOut>
    where TIn : class
    where TOut : class
{
    readonly TcpListener listener_;

    public IPEndPoint Local { get; }

    public TcpServerTransport(IPEndPoint local)
    {
        Local = local;
        listener_ = new(local);
        serverFinish_ = serverFinishSource_.Task;
    }

    public event Action<long, TIn>? OnMessage;
    public event Action<long>? OnClientJoin;
    public event Action<long>? OnClientFinish;
    public event Action<long, ClientFinishReason>? OnClientFinishReason;
    public event Action? OnFinish;

    readonly ConcurrentDictionary<long, Connection<TIn, TOut>> idToConnection_ = new();

    int running_ = 0;

    async Task ManageClientsAsync()
    {
        long id = 0;

        while (true)
        {
            id++;
            
            var acceptTask = listener_.AcceptTcpClientAsync();

            await Task.WhenAny(acceptTask, serverFinish_);

            TcpClient? client = acceptTask.Status == TaskStatus.RanToCompletion ? acceptTask.Result : null;
            
            if (serverFinish_.IsCompleted)
            {
                client?.Dispose();
                return;
            }

            if (client is null)
                continue;
            
            NetworkStream stream;

            try
            {
                stream = client.GetStream();
            }
            catch (InvalidOperationException)
            {
                client.Dispose();
                continue;
            }

            long idCapture = id;
            Connection<TIn, TOut> connection = new(client, stream, MessageHandler);

            if (!idToConnection_.TryAdd(id, connection))
            {
                TryFinishServer(ServerFinishReason.Fault).AssureSuccess();
                return;
            }

            connection.Run().ContinueWith(FinishHandler).AssureSuccess();
            
            OnClientJoin?.Invoke(id);
            continue;

            void MessageHandler(TIn message)
            {
                OnMessage?.Invoke(idCapture, message);
            }

            void FinishHandler(Task _)
            {
                if (!idToConnection_.TryRemove(idCapture, out var formerConnection))
                    throw new InvalidDataException("Given id was not registered.");

                Debug.Assert(formerConnection!.Finish.IsCompleted);

                OnClientFinish?.Invoke(idCapture);
                OnClientFinishReason?.Invoke(idCapture, TranslateReason(formerConnection.Finish.Result));
            }
        }
    }

    static ClientFinishReason TranslateReason(ConnectionFinishReason reason)
    {
        return reason switch
        {
            ConnectionFinishReason.Terminated => ClientFinishReason.Kicked,
            ConnectionFinishReason.OtherSideEnded => ClientFinishReason.Disconnect,
            ConnectionFinishReason.Faulted => ClientFinishReason.Faulted,
            ConnectionFinishReason.Corrupted => ClientFinishReason.Corrupted,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Invalid finish reason")
        };
    }

    readonly TaskCompletionSource<ServerFinishReason> serverFinishSource_ = new();
    readonly Task<ServerFinishReason> serverFinish_;

    Task? run_;

    async Task Run()
    {
        Task finishTask = serverFinishSource_.Task;
        Task manageTask = ManageClientsAsync();

        await Task.WhenAny(finishTask, manageTask);

        TryFinishServer(ServerFinishReason.Fault).AssureSuccess();

        await Task.WhenAny(manageTask); // assure the task completed in any way

        listener_.Stop();
    }

    public Task Start()
    {
        if (Interlocked.CompareExchange(ref running_, 1, 0) != 0)
            throw new InvalidOperationException("The transport is already running.");
        
        listener_.Start();

        run_ = Run();
        run_.ContinueWith(_ => OnFinish?.Invoke()).AssureSuccess();

        return Task.CompletedTask;
    }

    public void Terminate() => TryFinishServer(ServerFinishReason.Terminated).AssureSuccess();

    async Task TryFinishServer(ServerFinishReason reason)
    {
        if (run_ is null)
            throw new InvalidOperationException("Server did not start yet.");

        serverFinishSource_.TrySetResult(reason);
            
        await run_;

        foreach (var (_, connection) in idToConnection_)
            connection.TryFinish();
    }

    void Send(TOut message)
    {
        foreach (var (_, connection) in idToConnection_)
            connection.Send(message);
    }

    void Send(TOut message, long id)
    {
        if (serverFinish_.IsCompleted)
            return;

        if (idToConnection_.TryGetValue(id, out var connection))
            connection.Send(message);
    }

    public void SendReliable(TOut message) => Send(message);
    public void SendUnreliable(TOut message) => Send(message);
    public void SendReliable(TOut message, long id) => Send(message, id);
    public void SendUnreliable(TOut message, long id) => Send(message, id);

    public ServerFinishReason FinishReason
    {
        get
        {
            if (run_ is null || !run_.IsCompleted)
                throw new InvalidOperationException("The server did not finish yet.");

            return serverFinish_.Result;
        }
    }

    public void Terminate(long id)
    {
        if (serverFinish_.IsCompleted)
            return;

        if (idToConnection_.TryGetValue(id, out var connection))
            connection.TryFinish();
    }
}
