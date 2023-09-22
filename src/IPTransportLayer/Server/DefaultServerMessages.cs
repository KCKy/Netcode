using Core.Extensions;
using Core.Transport;
using MemoryPack;

namespace DefaultTransport.Server;

[MemoryPackable]
[MemoryPackUnion(0, typeof(AuthoritativeInputMessage))]
public partial interface IMessageToClient
{
    void Inform(IClientSession session);
}

[MemoryPackable]
public sealed partial class AuthoritativeInputMessage : IMessageToClient
{
    public long Frame;

    [MemoryPoolFormatter<byte>]
    public Memory<byte> Input;

    public long? Checksum;

    public void Inform(IClientSession session)
    {
        session.AddAuthoritativeInput(Frame, Input, Checksum);
        Input = null;
    }
}

public sealed partial class InitializationMessage : IMessageToClient
{
    public long Id;
    public long Frame;

    [MemoryPoolFormatter<byte>]
    public Memory<byte> State;

    public void Inform(IClientSession session)
    {
        session.Start(Id);
        session.Initialize(Frame, State);
        State = null;
    }
}
