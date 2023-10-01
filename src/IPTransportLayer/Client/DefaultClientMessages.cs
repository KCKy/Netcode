using MemoryPack;
using Core.Transport;

namespace DefaultTransport.Client;

[MemoryPackable]
[MemoryPackUnion(0, typeof(ClientInputMessage))]
public partial interface IMessageToServer
{
    void Inform(IServerSession session, long id);
}

[MemoryPackable]
public sealed partial class ClientInputMessage : IMessageToServer
{
    public long Frame;

    //[MemoryPoolFormatter<byte>]
    public Memory<byte> Input;

    public void Inform(IServerSession session, long id)
    {
        session.AddInput(id, Frame, Input);
        Input = null;
    }
}
