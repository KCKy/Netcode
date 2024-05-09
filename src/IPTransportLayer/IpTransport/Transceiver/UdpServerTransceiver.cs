using System;
using Serilog;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Kcky.Useful;

namespace Kcky.GameNewt.Transport.Default;

/// <summary>
/// Implements sending and receiving messages over a UDP socket for the server.
/// Designed to work with <see cref="IPendingMessages{T}"/> and <see cref="Receiver{T}"/>.
/// </summary>
sealed class UdpServerTransceiver : IProtocol<(Memory<byte> payload, int id), (Memory<byte> payload, int? id)>
{
    readonly Socket client_;
    readonly IPEndPoint ipPoint_ = new(IPAddress.Loopback, 0);
    readonly ConcurrentDictionary<int, ConnectedClient> idToConnection_;
    readonly ILogger logger_ = Log.ForContext<UdpServerTransceiver>();

    public UdpServerTransceiver(Socket client, ConcurrentDictionary<int, ConnectedClient> idToConnection)
    {
        client_ = client;
        idToConnection_ = idToConnection;
    }

    public const int HeaderSize = 0;

    public const int ConnectionResetByPeer = 10054;

    public async ValueTask<(Memory<byte> payload, int id)> ReceiveAsync(CancellationToken cancellation)
    {
        var buffer = ArrayPool<byte>.Shared.RentMemory(Udp.MaxDatagramSize);
        const int headerSize = sizeof(int);

        while (true)
        {
            SocketReceiveFromResult result;

            try
            {
                result = await client_.ReceiveFromAsync(buffer, ipPoint_, cancellation);
            }
            catch (SocketException ex)
            {
                if (ex.ErrorCode == ConnectionResetByPeer)
                {
                    // This error seems to make no sense, it's not the servers fault the other side closed.
                    // SOURCE: https://stackoverflow.com/questions/34242622/windows-udp-sockets-recvfrom-fails-with-error-10054
                    logger_.Warning("Received error {Code} on UDP receive.", ex.ErrorCode);
                    continue;
                }
                
                throw;
            }

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

            int id = Bits.ReadInt(buffer[..headerSize].Span);

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

            /*
             * Nowadays, most clients are going to be behind a masquerade NAT. The socket of the UDP connection may be changed during the connection
             * (because UDP is stateless). To make this work we check only the IP address and change the target port when it seems to have changed.
             * TCP does not have the same problem as the state of a TCP connection is tracked by the router.
             */

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

    public async ValueTask SendAsync((Memory<byte> payload, int? id) value, CancellationToken cancellation)
    {
        (var payload, int? id) = value;

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
}
