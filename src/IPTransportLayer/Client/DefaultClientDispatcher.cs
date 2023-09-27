using Core.Transport;

namespace DefaultTransport.Client;

public sealed class DefaultClientDispatcher : IClientDispatcher
{
    readonly IClientOutTransport<IMessageToServer> outTransport_;

    public DefaultClientDispatcher(IClientOutTransport<IMessageToServer> outTransport)
    {
        outTransport_ = outTransport;
    }

    public void Disconnect()
    {
        outTransport_.Terminate();
    }

    public void SendInput(long frame, Memory<byte> input)
    {
        ClientInputMessage message = new()
        {
            Input = input,
            Frame = frame
        };

        outTransport_.SendReliable(message);
    }
}
