using Core.Extensions;
using Core.Utility;
using Serilog;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;

namespace DefaultTransport.IpTransport;

class UdpServerTransceiver : IProtocol<(Memory<byte> payload, long id), (Memory<byte> payload, long? id)>
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
                logger_.Verbose("Received an unreliable message too short.");
                continue;
            }

            if (result.RemoteEndPoint is not IPEndPoint valid)
            {
                logger_.Verbose("Received unreliable from invalid endpoint.");
                continue;
            }

            long id = Bits.ReadLong(buffer[..headerSize].Span);

            if (!idToConnection_.TryGetValue(id, out ConnectedClient? client))
            {
                logger_.Verbose("Received unreliable with invalid id {Id}", id);
                continue;
            }

            IPEndPoint used = client.UdpTarget;

            IPAddress address = used.Address;
            if (!address.Equals(valid.Address))
            {
                logger_.Verbose("Received unreliable message for {Id} comes from different address {Used} != {Valid}", id, address, valid.Address);
                continue; // Address does not match
            }

            int port = valid.Port;
            int usedPort = used.Port;
            if (usedPort != port)
            {
                // Client seems to have changed port
                client.UdpTarget = new(address, port);
                logger_.Verbose("Different port with unreliable was used for {Id}: {Used} != {Valid}. Updating.", id, usedPort, port);
            }

            return (buffer[headerSize..length], id);
        }
    }

    ValueTask<int> SendToTargetAsync(ConnectedClient client, Memory<byte> payload, CancellationToken cancellation)
    {
        IPEndPoint target = client.UdpTarget;
        var task = client_.SendToAsync(payload, client.UdpTarget, cancellation);
        logger_.Verbose("Sent unreliable message to target {Target} with length {Length}.", target, payload.Length);
        return task;
    }

    public async ValueTask SendAsync((Memory<byte> payload, long? id) value, CancellationToken cancellation)
    {
        (var payload, long? id) = value;

        if (id is {} valid)
        {
            if (idToConnection_.TryGetValue(valid, out ConnectedClient? client))
                await SendToTargetAsync(client, payload, cancellation);
        }
        else
        {
            foreach ((_, ConnectedClient client) in idToConnection_)
                await SendToTargetAsync(client, payload, cancellation);
        }

        value.payload.ReturnToArrayPool();
    }
    
    public Task CloseAsync()
    {
        client_.Dispose();
        return Task.CompletedTask;
    }
}
