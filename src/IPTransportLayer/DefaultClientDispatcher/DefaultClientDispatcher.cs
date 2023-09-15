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
        ClientInputMessage message = ObjectPool<ClientInputMessage>.Create();

        message.Input = input;
        message.Frame = frame;

        outTransport_.SendReliable(message);

        message.Input.ReturnToArrayPool();
        message.Destroy();
    }
}
