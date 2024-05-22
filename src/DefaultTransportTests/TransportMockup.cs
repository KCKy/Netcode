using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Kcky.Useful;

namespace Kcky.GameNewt.Transport.Default.Tests;

sealed class MockServerTransport(int messageHeadersLength, int unreliableMaxLength) : IServerTransport
{
    public event ServerMessageEvent? OnReliableMessage;
    public event ServerMessageEvent? OnUnreliableMessage;
    public event Action<int>? OnClientJoin;
    public event Action<int>? OnClientFinish;
    public int ReliableMessageHeader => messageHeadersLength;
    public int UnreliableMessageHeader => messageHeadersLength;
    public int UnreliableMessageMaxLength => unreliableMaxLength;

    enum State
    {
        Unstarted,
        Running,
        Terminated
    }

    sealed class MockClientTransport(int messageHeadersLength, int unreliableMaxLength, Action connectedCallback, Action terminatedCallback, Action<Memory<byte>> reliableCallback, Action<Memory<byte>> unreliableCallback) : IClientTransport
    {
        public event ClientMessageEvent? OnReliableMessage;
        public event ClientMessageEvent? OnUnreliableMessage;
        public int ReliableMessageHeader => messageHeadersLength;
        public int UnreliableMessageHeader => messageHeadersLength;
        public int UnreliableMessageMaxLength => unreliableMaxLength;
        
        public void SendReliable(Memory<byte> message)
        {
            lock (mutex_)
                if (state_ != State.Running)
                    return;

            reliableCallback.Invoke(message[ReliableMessageHeader..]);
        }

        public void SendUnreliable(Memory<byte> message)
        {
            lock (mutex_)
                if (state_ != State.Running)
                    return;

            reliableCallback.Invoke(message[UnreliableMessageHeader..]);
        }

        public void ReceiveReliable(Memory<byte> message)
        {
            lock (mutex_)
                if (state_ != State.Running)
                    return;

            OnReliableMessage?.Invoke(message);
        }

        public void ReceiveUnreliable(Memory<byte> message)
        {
            lock (mutex_)
                if (state_ != State.Running)
                    return;

            OnUnreliableMessage?.Invoke(message);
        }

        readonly object mutex_ = new();
        State state_ = State.Unstarted;

        public void Terminate()
        {
            lock (mutex_)
            {
                switch (state_)
                {
                    case State.Unstarted:
                        state_ = State.Terminated;
                        return;
                    case State.Running:
                        state_ = State.Terminated;
                        runtime_.SetCanceled();
                        terminatedCallback();
                        return;
                    case State.Terminated:
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        readonly TaskCompletionSource runtime_ = new();

        public Task RunAsync()
        {
            lock (mutex_)
            {
                switch (state_)
                {
                    case State.Unstarted:
                        state_ = State.Running;
                        connectedCallback();
                        return runtime_.Task;
                    case State.Running:
                        throw new InvalidOperationException();
                    case State.Terminated:
                        return Task.CompletedTask;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }

    readonly ConcurrentDictionary<int, MockClientTransport> idToClient_ = new();

    public IClientTransport CreateMockClient(int id)
    {
        int idCapture = id;
        MockClientTransport client = new(ReliableMessageHeader, UnreliableMessageMaxLength,
            () => OnClientJoin?.Invoke(idCapture),
            () => OnClientFinish?.Invoke(idCapture),
            m => OnReliableMessage?.Invoke(idCapture, m),
            m => OnUnreliableMessage?.Invoke(idCapture, m));

        if (!idToClient_.TryAdd(id, client))
            throw new InvalidOperationException();

        return client;
    }

    public Task RunAsync() => Task.Delay(Timeout.InfiniteTimeSpan);
    public void Terminate() => throw new InvalidOperationException();
    
    public void SendReliable(Memory<byte> message)
    {
        foreach (MockClientTransport client in idToClient_.Values)
        {
            Memory<byte> copy = ArrayPool<byte>.Shared.RentMemory(message.Length);
            message.CopyTo(copy);
            client.ReceiveReliable(copy[ReliableMessageHeader..]);
        }
    }

    public void SendReliable(Memory<byte> message, int id)
    {
        if (idToClient_.TryGetValue(id, out var client))
            client.ReceiveReliable(message[ReliableMessageHeader..]);
    }

    public void SendUnreliable(Memory<byte> message)
    {
        foreach (MockClientTransport client in idToClient_.Values)
        {
            Memory<byte> copy = ArrayPool<byte>.Shared.RentMemory(message.Length);
            message.CopyTo(copy);
            client.ReceiveUnreliable(copy[UnreliableMessageHeader..]);
        }
    }

    public void SendUnreliable(Memory<byte> message, int id)
    {
        if (idToClient_.TryGetValue(id, out MockClientTransport? client))
            client.ReceiveUnreliable(message[UnreliableMessageHeader..]);
    }

    public void Kick(int id)
    {
        if (idToClient_.TryGetValue(id, out MockClientTransport? client))
            client.Terminate();
    }
}


sealed class SingleClientMockTransport : IClientTransport
{
    public event ClientMessageEvent? OnReliableMessage;
    public event ClientMessageEvent? OnUnreliableMessage;
    public void InvokeReliable(Memory<byte> message) => OnReliableMessage?.Invoke(message);
    public void InvokeUnreliable(Memory<byte> message) => OnUnreliableMessage?.Invoke(message);
    public int ReliableMessageHeader { get; init; } = 0;
    public void SendReliable(Memory<byte> message) => throw new InvalidOperationException();
    public int UnreliableMessageHeader { get; init; } = 0;
    public int UnreliableMessageMaxLength { get; init; } = int.MaxValue;
    public void SendUnreliable(Memory<byte> message)  => throw new InvalidOperationException();

    readonly TaskCompletionSource runtime_ = new();
    public void Terminate() => runtime_.SetCanceled();
    public Task RunAsync() => runtime_.Task;
}

sealed class SingleServerMockTransport : IServerTransport
{
    public event ServerMessageEvent? OnReliableMessage;
    public event ServerMessageEvent? OnUnreliableMessage;
    public void InvokeReliable(int id, Memory<byte> message) => OnReliableMessage?.Invoke(id, message);
    public void InvokeUnreliable(int id, Memory<byte> message) => OnUnreliableMessage?.Invoke(id, message);

    public event Action<int>? OnClientJoin;
    public event Action<int>? OnClientFinish;
    public int ReliableMessageHeader { get; init; } = 0;
    public void SendReliable(Memory<byte> message) => throw new InvalidOperationException();
    public void SendReliable(Memory<byte> message, int id) => throw new InvalidOperationException();
    public int UnreliableMessageHeader { get; init; } = 0;
    public int UnreliableMessageMaxLength { get; } = int.MaxValue;
    public void SendUnreliable(Memory<byte> message) => throw new InvalidOperationException();
    public void SendUnreliable(Memory<byte> message, int id) => throw new InvalidOperationException();
    public void Kick(int id) => throw new InvalidOperationException();
    public void Kick() => throw new InvalidOperationException();

    readonly TaskCompletionSource runtime_ = new();
    public void Terminate() => runtime_.SetCanceled();
    public Task RunAsync() => runtime_.Task;
}

