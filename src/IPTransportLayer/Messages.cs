using Core;
using Core.Server;
using MemoryPack;
using Core.Extensions;

namespace IPTransportLayer;

[MemoryPackable]
[MemoryPackUnion(0, typeof(ClientInputMessage))]
public partial interface IMessageToServer : IDisposable
{
    void Inform(IServerSession session, long id);
}

[MemoryPackable]
[MemoryPackUnion(0, typeof(AuthoritativeInputMessage))]
public partial interface IMessageToClient : IDisposable
{
    void Inform(IClientSession session);
}

public sealed partial class MultiMessageToServer : IMessageToServer
{
    [MemoryPoolFormatter<byte>]
    public Memory<IMessageToServer> messages = Array.Empty<IMessageToServer>();

    public void Dispose()
    {
        foreach (var message in messages.Span)
            message.Dispose();

        messages.ReturnToArrayPool();
    }

    public void Inform(IServerSession session, long id)
    {
        foreach (var message in messages.Span)
            message.Inform(session, id);
    }
}

public sealed partial class MultiMessageToClient: IMessageToClient
{
    [MemoryPoolFormatter<byte>]
    public Memory<IMessageToClient> messages = Array.Empty<IMessageToClient>();

    public void Dispose()
    {
        foreach (var message in messages.Span)
            message.Dispose();

        messages.ReturnToArrayPool();
    }

    public void Inform(IClientSession session)
    {
        foreach (var message in messages.Span)
            message.Inform(session);

    }
}

[MemoryPackable]
public sealed partial class ClientInputMessage : IMessageToServer
{
    public long Frame;

    [MemoryPoolFormatter<byte>]
    Memory<byte> Input;

    public void Dispose()
    {
        Input.ReturnToArrayPool();
    }

    public void Inform(IServerSession session, long id)
    {
        throw new NotImplementedException();
    }
}

[MemoryPackable]
public sealed partial class AuthoritativeInputMessage : IMessageToClient
{
    public long Frame;

    [MemoryPoolFormatter<byte>]
    public Memory<byte> Input;

    public long? Checksum;

    public void Dispose()
    {
        Input.ReturnToArrayPool();
    }

    public void Inform(IClientSession session)
    {
        throw new NotImplementedException();
    }
}

public sealed partial class InitializationMessage : IMessageToClient
{
    public long Id;
    public long Frame;

    [MemoryPoolFormatter<byte>]
    public Memory<byte> State;

    public void Dispose()
    {
        State.ReturnToArrayPool();
    }

    public void Inform(IClientSession session)
    {
        throw new NotImplementedException();
    }
}

/*
public sealed class IpClientTransport : ITransport<IMessageToClient, IMessageToServer>
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void SendMessageReliable(IMessageToServer message)
    {
        throw new NotImplementedException();
    }

    public void SendMessageUnreliable(IMessageToServer payload)
    {
        throw new NotImplementedException();
    }

    public Task Run()
    {
        throw new NotImplementedException();
    }

    public event Action<IMessageToClient>? OnReceivedMessage;
}

public sealed class IpServerTransport : ITransport<IMessageToClient, IMessageToServer>
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void SendMessageReliable(IMessageToServer message)
    {
        throw new NotImplementedException();
    }

    public void SendMessageUnreliable(IMessageToServer payload)
    {
        throw new NotImplementedException();
    }

    public Task Run()
    {
        throw new NotImplementedException();
    }

    public event Action<IMessageToClient>? OnReceivedMessage;
}
*/