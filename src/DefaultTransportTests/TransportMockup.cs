using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Kcky.Useful;

namespace Kcky.GameNewt.Transport.Default.Tests;

sealed class MockClientTransport : IClientTransport
{
    public event ClientMessageEvent? OnReliableMessage;
    public event ClientMessageEvent? OnUnreliableMessage;

    internal void InvokeReliable(Memory<byte> message) => OnReliableMessage?.Invoke(message[ReliableMessageHeader..]);
    internal void InvokeUnreliable(Memory<byte> message) => OnUnreliableMessage?.Invoke(message[UnreliableMessageHeader..]);
    
    MockServerTransport? transport_;
    internal readonly int Id;

    public MockClientTransport(int id)
    {
        Id = id;
    }

    internal void Register(MockServerTransport transport) => transport_ = transport;

    public void Start() => transport_?.InvokeJoin(this);
    public void Terminate() => transport_?.InvokeFinish(Id);

    public int ReliableMessageHeader { get; init; } = 0;
    public int UnreliableMessageHeader { get; init; } = 0;
    public int UnreliableMessageMaxLength { get; init; } = int.MaxValue;

    public void SendReliable(Memory<byte> message) => transport_?.InvokeReliable(Id, message);
    public void SendUnreliable(Memory<byte> message) => transport_?.InvokeUnreliable(Id, message);
}

sealed class MockServerTransport : IServerTransport, IEnumerable<MockClientTransport>
{
    public event ServerMessageEvent? OnReliableMessage;
    public event ServerMessageEvent? OnUnreliableMessage;
    public event Action<int>? OnClientJoin;
    public event Action<int>? OnClientFinish;

    internal void InvokeReliable(int id, Memory<byte> message)
    {
        if (idToClient_.TryGetValue(id, out _))
            OnReliableMessage?.Invoke(id, message);
    }

    internal void InvokeUnreliable(int id, Memory<byte> message)
    {
        if (idToClient_.TryGetValue(id, out _))
            OnUnreliableMessage?.Invoke(id, message);
    }

    internal void InvokeJoin(MockClientTransport client)
    {
        if (idToClient_.TryAdd(client.Id, client))
            OnClientJoin?.Invoke(client.Id);
    }

    internal void InvokeFinish(int id)
    {
        if (idToClient_.TryRemove(id, out _))
            OnClientFinish?.Invoke(id);
    }

    readonly ConcurrentDictionary<int, MockClientTransport> idToClient_ = new();

    public void Add(MockClientTransport client)
    {
        client.Register(this);
    }

    public int ReliableMessageHeader { get; init; } = 0;
    public int UnreliableMessageHeader { get; init; } = 0;
    public int UnreliableMessageMaxLength { get; init; } = int.MaxValue;

    public void SendReliable(Memory<byte> message)
    {
        foreach (var pair in idToClient_)
        {
            var copy = ArrayPool<byte>.Shared.RentMemory(message.Length);
            message.CopyTo(copy);
            pair.Value.InvokeReliable(copy[ReliableMessageHeader..]);
        }

        ArrayPool<byte>.Shared.Return(message);
    }

    public void SendReliable(Memory<byte> message, int id)
    {
        if (idToClient_.TryGetValue(id, out var client))
            client.InvokeReliable(message[ReliableMessageHeader..]);
    }
    
    public void SendUnreliable(Memory<byte> message)
    {
        foreach (var pair in idToClient_)
        {
            var copy = ArrayPool<byte>.Shared.RentMemory(message.Length);
            message.CopyTo(copy);
            pair.Value.InvokeUnreliable(copy[UnreliableMessageHeader..]);
        }

        ArrayPool<byte>.Shared.Return(message);
    }

    public void SendUnreliable(Memory<byte> message, int id)
    {
        if (idToClient_.TryGetValue(id, out var client))
            client.InvokeUnreliable(message[UnreliableMessageHeader..]);
    }

    public void Kick(int id) => InvokeFinish(id);
    public void Kick() => throw new InvalidOperationException();

    public IEnumerator<MockClientTransport> GetEnumerator() => (from p in idToClient_ select p.Value).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

sealed class SingleClientMockTransport : IClientTransport
{
    public event ClientMessageEvent? OnReliableMessage;
    public event ClientMessageEvent? OnUnreliableMessage;
    public void InvokeReliable(Memory<byte> message) => OnReliableMessage?.Invoke(message);
    public void InvokeUnreliable(Memory<byte> message) => OnUnreliableMessage?.Invoke(message);
    public void Terminate()  => throw new InvalidOperationException();
    public int ReliableMessageHeader { get; init; } = 0;
    public void SendReliable(Memory<byte> message) => throw new InvalidOperationException();
    public int UnreliableMessageHeader { get; init; } = 0;
    public int UnreliableMessageMaxLength { get; init; } = int.MaxValue;
    public void SendUnreliable(Memory<byte> message)  => throw new InvalidOperationException();
}

sealed class SingleServerMockTransport : IServerTransport
{
    public event ServerMessageEvent? OnReliableMessage;
    public event ServerMessageEvent? OnUnreliableMessage;
    public void InvokeReliable(int id, Memory<byte> message) => OnReliableMessage?.Invoke(id, message);
    public void InvokeUnreliable(int id, Memory<byte> message) => OnUnreliableMessage?.Invoke(id, message);

    public event Action<int>? OnClientJoin;
    public event Action<int>? OnClientFinish;
    public void InvokeJoin(int id) => OnClientJoin?.Invoke(id);
    public void InvokeFinish(int id) => OnClientFinish?.Invoke(id);
    public int ReliableMessageHeader { get; init; } = 0;
    public void SendReliable(Memory<byte> message) => throw new InvalidOperationException();
    public void SendReliable(Memory<byte> message, int id) => throw new InvalidOperationException();
    public int UnreliableMessageHeader { get; init; } = 0;
    public int UnreliableMessageMaxLength { get; } = int.MaxValue;
    public void SendUnreliable(Memory<byte> message) => throw new InvalidOperationException();
    public void SendUnreliable(Memory<byte> message, int id) => throw new InvalidOperationException();
    public void Kick(int id) => throw new InvalidOperationException();
    public void Kick() => throw new InvalidOperationException();
}
