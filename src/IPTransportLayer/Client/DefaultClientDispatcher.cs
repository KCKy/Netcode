using System.Buffers;
using Core.Extensions;
using Core.Transport;

namespace DefaultTransport.Client;

public sealed class DefaultClientDispatcher : IClientDispatcher
{
    readonly IClientOutTransport outTransport_;

    public DefaultClientDispatcher(IClientOutTransport outTransport)
    {
        outTransport_ = outTransport;
    }

    public void Disconnect() => outTransport_.Terminate();

    public void SendInput(long frame, Memory<byte> input)
    {
        const int inputPosition = DefaultApplicationProtocol.HeaderSize + sizeof(long);

        int payloadSize = sizeof(long) + input.Length;
        var packet = DefaultApplicationProtocol.PreparePacket(Messages.ClientInput, payloadSize);
        
        input.CopyTo(packet[inputPosition..]);
        input.ReturnToArrayPool();
    }
}
