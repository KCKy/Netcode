using Core.Transport;

namespace DefaultTransport.Server;

public sealed class DefaultServerDispatcher : IServerDispatcher
{
    readonly IServerOutTransport<IMessageToClient> outTransport_;

    public DefaultServerDispatcher(IServerOutTransport<IMessageToClient> outTransport)
    {
        outTransport_ = outTransport;
    }

    public void Initialize(long id, long frame, Memory<byte> state)
    {
        InitializationMessage message = new()
        {
            Id = id,
            Frame = frame,
            State = state
        };

        outTransport_.SendReliable(message, id);
    }

    public void InputAuthored(long id, long frame, TimeSpan difference)
    {
        DelayInfoMessage message = new()
        {
            Frame = frame,
            DelayMs = difference.TotalMilliseconds,
        };

        outTransport_.SendUnreliable(message, id);
    }

    public void Kick(long id)
    {
        outTransport_.Terminate(id);
    }

    public void SendAuthoritativeInput(long frame, Memory<byte> input, long? checksum)
    {
        AuthoritativeInputMessage message = new()
        {
            Frame = frame,
            Input = input,
            Checksum = checksum
        };
        // TODO: send aggregate inputs

        outTransport_.SendReliable(message);
    }
}
