using System.Net;
using Core.Transport.Client;
using System.Net.Sockets;
using Core.Extensions;

namespace DefaultTransport.TcpTransport;

// TODO: add more logging

public enum ClientFinishReason
{
    Unknown = 0,
    Disconnect,
    Faulted,
    Corrupted,
    Kicked
}

public sealed class TcpClientTransport<TIn, TOut> : IClientTransport<TIn, TOut>
    where TIn : class
    where TOut : class
{
    public required IPEndPoint Target { get; init; }

    public event Action? OnFinish;
    public event Action<TIn>? OnMessage;

    int running_ = 0;
    Connection<TIn, TOut>? connection_;

    Task? run_;

    public async Task Start()
    {
        if (Interlocked.CompareExchange(ref running_, 1, 0) != 0)
            throw new InvalidOperationException("The transport is already running.");

        TcpClient client = new();
        NetworkStream stream;

        await client.ConnectAsync(Target);

        try
        {
            stream = client.GetStream();
        }
        catch (InvalidOperationException)
        {
            client.Dispose();
            throw;
        }

        connection_ = new (client, stream, m => OnMessage?.Invoke(m));

        run_ = connection_.Run();
        run_.ContinueWith(_ => OnFinish?.Invoke()).AssureSuccess();
    }

    void Send(TOut message)
    {
        var connection = connection_ ?? throw new InvalidOperationException("The client is not started yet.");
        connection.Send(message);
    }
    
    public void SendReliable(TOut message) => Send(message);

    public void SendUnreliable(TOut message) => Send(message);

    public ClientFinishReason FinishReason
    {
        get
        {
            var connection = connection_;

            if (connection == null || run_ is null || !run_.IsCompleted)
                throw new InvalidOperationException("The client did not finish yet.");

            return TranslateReason(connection.Finish.Result);
        }
    }

    static ClientFinishReason TranslateReason(ConnectionFinishReason reason)
    {
        return reason switch
        {
            ConnectionFinishReason.Terminated => ClientFinishReason.Disconnect,
            ConnectionFinishReason.OtherSideEnded => ClientFinishReason.Kicked,
            ConnectionFinishReason.Faulted => ClientFinishReason.Faulted,
            ConnectionFinishReason.Corrupted => ClientFinishReason.Corrupted,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Invalid finish reason")
        };
    }

    public void Terminate()
    {
        var connection = connection_ ?? throw new InvalidOperationException("The client did not start yet.");
        connection.TryFinish();
    }
}

// TODO: what about kicking
