using Core.Transport;

namespace DefaultTransport.Server;

public sealed class DefaultServerDispatcher : IServerDispatcher
{
    readonly IServerOutTransport outTransport_;

    public DefaultServerDispatcher(IServerOutTransport outTransport)
    {
        outTransport_ = outTransport;
    }

    public void Kick(long id) => outTransport_.Terminate(id);
    
    public void Initialize(long id, long frame, Memory<byte> state)
    {
        throw new NotImplementedException();
    }

    public void InputAuthored(long id, long frame, TimeSpan difference)
    {
        throw new NotImplementedException();
    }
    
    public void SendAuthoritativeInput(long frame, Memory<byte> input, long? checksum)
    {
        throw new NotImplementedException();
    }
}
