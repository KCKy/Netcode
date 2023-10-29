using Serilog;
using Serilog;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using Useful;

namespace DefaultTransport.IpTransport;

sealed class UdpServerTransceiver : IProtocol<(Memory<byte> payload, long id), (Memory<byte> payload, long? id)>
{
    readonly Socket client_;
    readonly IPEndPoint ipPoint_ = new(IPAddress.Loopback, 0);
    readonly ConcurrentDictionary<long, ConnectedClient> idToConnection_;
    readonly ILogger logger_ = Log.ForContext<UdpServerTransceiver>();

    public UdpServerTransceiver(Socket client, ConcurrentDictionary<long, ConnectedClient> idToConnection)
    {
        client_ = client;
        idToConnection_ = idToConnection;
    }

    public const int HeaderSize = 0;

    public async ValueTask<(Memory<byte> payload, long id)> ReceiveAsync(CancellationToken cancellation)
    {
        var buffer = ArrayPool<byte>.Shared.RentMemory(Udp.MaxDatagramSize);
        const int headerSize = sizeof(long);

        while (true)
        {
            SocketReceiveFromResult result = await client_.ReceiveFromAsync(buffer, ipPoint_, cancellation);

            int length = result.ReceivedBytes;

            logger_.Verbose("Received unreliable message of length {Length}.", length);

            if (length < headerSize)
            {
                logger_.Verbose("The message is too short.");
                continue;
            }

            if (result.RemoteEndPoint is not IPEndPoint sender)
            {
                logger_.Verbose("It is from invalid endpoint.");
                continue;
            }

            logger_.Verbose("It came from {MemorySender}.", sender);

            long id = Bits.ReadLong(buffer[..headerSize].Span);

            if (!idToConnection_.TryGetValue(id, out ConnectedClient? client))
            {
                logger_.Verbose("It has invalid id {Id}", id);
                continue;
            }

            logger_.Verbose("It has id {Id}.", id);

            IPEndPoint current = client.UdpTarget;

            IPAddress address = current.Address;
            if (!address.Equals(sender.Address))
            {
                logger_.Verbose("It comes from different address {Used} != {Valid}.", id, address, sender.Address);
                continue; // Address does not match
            }

            int senderPort = sender.Port;
            int currentPort = current.Port;
            if (currentPort != senderPort)
            {
                // Client seems to have changed port
                client.UdpTarget = new(address, senderPort);
                logger_.Verbose("Different port with unreliable was used : {Used} != {Valid}. Updating.", currentPort, senderPort);
            }
            
            return (buffer[headerSize..length], id);
        }
    }

    ValueTask<int> SendToTargetAsync(IPEndPoint target, Memory<byte> payload, CancellationToken cancellation)
    {
        var task = client_.SendToAsync(payload, target, cancellation);
        logger_.Verbose("Sent unreliable message to target {Target} with length {Length}.", target, payload.Length);
        return task;
    }

    public async ValueTask SendAsync((Memory<byte> payload, long? id) value, CancellationToken cancellation)
    {
        (var payload, long? id) = value;

        if (id is {} valid)
        {
            if (idToConnection_.TryGetValue(valid, out ConnectedClient? client))
                await SendToTargetAsync(client.UdpTarget, payload, cancellation);
        }
        else
        {
            bool sent = false;

            foreach ((_, ConnectedClient client) in idToConnection_)
            {
                await SendToTargetAsync(client.UdpTarget, payload, cancellation);
                sent = true;
            }

            if (!sent)
                logger_.Verbose("Got no target to send unreliable broadcast to.");
        }

        ArrayPool<byte>.Shared.Return(value.payload);
    }
    
    public Task CloseAsync()
    {
        client_.Dispose();
        return Task.CompletedTask;
    }
}
