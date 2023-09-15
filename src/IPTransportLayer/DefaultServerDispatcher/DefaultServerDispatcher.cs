using Core.Extensions;
using Core.Transport;
using Core.Utility;

namespace DefaultTransport.DefaultServerDispatcher;

public class DefaultServerDispatcher : IServerDispatcher
{
    readonly IServerOutTransport<IMessageToClient> outTransport_;

    public DefaultServerDispatcher(IServerOutTransport<IMessageToClient> outTransport)
    {
        outTransport_ = outTransport;
    }

    public void Initialize(long id, long frame, Memory<byte> state)
    {
        var message = ObjectPool<InitializationMessage>.Create();
        
        message.Id = id;
        message.Frame = frame;
        message.State = state;
        
        outTransport_.SendReliable(message, id);

        message.State.ReturnToArrayPool();
        ObjectPool<InitializationMessage>.Destroy(message);
    }

    public void Kick(long id)
    {
        outTransport_.Terminate(id);
    }

    public void SendAuthoritativeInput(long frame, Memory<byte> input, long? checksum)
    {
        var message = ObjectPool<AuthoritativeInputMessage>.Create();

        message.Frame = frame;
        message.Input = input;
        message.Checksum = checksum;

        // TODO: send aggregate inputs

        outTransport_.SendReliable(message);
        
        message.Input.ReturnToArrayPool();
        message.Destroy();
    }
}
