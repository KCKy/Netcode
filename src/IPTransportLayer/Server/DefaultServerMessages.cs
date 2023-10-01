using Core.Transport;
using MemoryPack;

namespace DefaultTransport.Server;

[MemoryPackable]
[MemoryPackUnion(0, typeof(AuthoritativeInputMessage))]
[MemoryPackUnion(1, typeof(InitializationMessage))]
[MemoryPackUnion(2, typeof(DelayInfoMessage))]
public partial interface IMessageToClient
{
    void Inform(IClientSession session);
}

[MemoryPackable]
public sealed partial class AuthoritativeInputMessage : IMessageToClient
{
    public long Frame;

    //[MemoryPoolFormatter<byte>]
    public Memory<byte> Input;

    public long? Checksum;

    public void Inform(IClientSession session)
    {
        session.AddAuthoritativeInput(Frame, Input, Checksum);
        Input = null;
    }
}

[MemoryPackable]
public sealed partial class DelayInfoMessage : IMessageToClient
{
    public long Frame;
    public double DelayMs;

    public void Inform(IClientSession session)
    {
        double delay = DelayMs > 0 ? DelayMs : 0;

        // TODO: give an average

        session.SetDelay(delay);
    }
}

[MemoryPackable]
public sealed partial class InitializationMessage : IMessageToClient
{
    public long Id;
    public long Frame;

    //[MemoryPoolFormatter<byte>]
    public Memory<byte> State;

    public void Inform(IClientSession session)
    {
        session.Start(Id);
        session.Initialize(Frame, State);
        State = null;
    }
}
