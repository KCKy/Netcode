using Core;
using Core.Extensions;
using Core.Transport;
using Core.Transport.Client;
using Core.Utility;
using System.Diagnostics;

namespace DefaultTransport.DefaultClientDispatcher;

public class DefaultClientDispatcher : IClientDispatcher
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
