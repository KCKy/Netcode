using System;
using Core.Transport;
using System.Buffers;
using Core.Utility;
using MemoryPack;
using Serilog;
using Useful;

namespace DefaultTransport.Dispatcher;

/// <summary>
/// Implementation for the server-side part of the default transport protocol (<see cref="IServerSender"/>, <see cref="IServerReceiver"/>)
/// over a <see cref="IServerTransport"/>.
/// </summary>
/// <remarks>
/// For the server side check <see cref="DefaultClientDispatcher"/>.
/// </remarks>
public sealed class DefaultServerDispatcher : IServerSender, IServerReceiver
{
    readonly IServerTransport transport_;

    readonly int unreliableHeader_;
    readonly int reliableHeader_;
    readonly ILogger logger_ = Log.ForContext<DefaultServerDispatcher>();

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="transport">The transport which shall be used to send and receive packets.</param>
    public DefaultServerDispatcher(IServerTransport transport)
    {
        transport_ = transport;

        unreliableHeader_ = transport.UnreliableMessageHeader;
        reliableHeader_ = transport.ReliableMessageHeader;

        transport.OnUnreliableMessage += HandleMessage;
        transport.OnReliableMessage += HandleMessage;
    }

    /// <inheritdoc/>
    public void Kick(long id) => transport_.Terminate(id);

    /// <inheritdoc/>
    public event AddInputDelegate? OnAddInput;
    
    /// <inheritdoc/>
    public event Action<long> OnAddClient
    {
        add => transport_.OnClientJoin += value;
        remove => transport_.OnClientJoin -= value;
    }
    
    /// <inheritdoc/>
    public event Action<long> OnRemoveClient
    {
        add => transport_.OnClientFinish += value;
        remove => transport_.OnClientFinish -= value;
    }

    void HandleMessage(long id, Memory<byte> message)
    {
        if (message.IsEmpty)
        {
            logger_.Error("Received invalid empty message.");
            ArrayPool<byte>.Shared.Return(message);
            return;
        }

        var type = (MessageType)message.Span[0];
        message = message[1..];

        switch (type)
        {
            case MessageType.ClientInput:
                HandleClientInput(id, message);
                return;
            default:
                logger_.Error("Received invalid message from client {Id}: {Message} of type {Type}.", id, message, type);
                ArrayPool<byte>.Shared.Return(message);
                return;
        }
    }

    void HandleClientInput(long id, Memory<byte> owner)
    {
        var message = owner.Span;
        const int headerLength = DefaultClientDispatcher.InputStructHeader;

        while (message.Length > headerLength)
        {
            var header = message[..headerLength];
            long frame = header.ReadLong();
            int length = header.ReadInt();

            message = message[headerLength..];

            // We are checking if the promised message length is valid (is a natural number and does not overflow the rest of the buffer)
            if (length <= 0 || message.Length < length)
            {
                logger_.Error("Input in aggregate has invalid specified length: {Length} > {MessageLength}.", length, message.Length);
                break;
            }

            OnAddInput?.Invoke(id, frame, message[..length]);

            message = message[length..];
        }

        if (message.Length != 0)
            logger_.Error("Input aggregate has trailing data of length {Length}.", message.Length);

        ArrayPool<byte>.Shared.Return(owner);
    }

    static void WriteMessageHeader(PooledBufferWriter<byte> writer, int preHeader, MessageType type)
    {
        /*
         * Header format:
         * [ Pre-Header ] [ Type: byte ]
         */

        writer.Skip(preHeader);
        writer.GetSpan(1)[0] = (byte)type;
        writer.Advance(1);
    }

    // Initialize
    readonly PooledBufferWriter<byte> initializeBuffer_ = new();
    internal const int InitializeHeader = sizeof(long) * 2;
    Memory<byte> ConstructInitialize<TInitializePayload>(long id, long frame, TInitializePayload payload)
    {
        WriteMessageHeader(initializeBuffer_, reliableHeader_, MessageType.ServerInitialize);
        initializeBuffer_.Write(id);
        initializeBuffer_.Write(frame);
        MemoryPackSerializer.Serialize(initializeBuffer_, payload);
        return initializeBuffer_.ExtractAndReplace();
    }

    /// <inheritdoc/>
    public void Initialize<TPayload>(long id, long frame, TPayload payload)
    {
        /*
         * Packet format:
         * [ Reliable Message Header ] [ Message Type: byte ] [ ID: long ] [ Frame: long ] [ Payload: byte[] ]
         */

        var message = ConstructInitialize(id, frame, payload);
        transport_.SendReliable(message, id);
    }

    // Authorize
    readonly PooledBufferWriter<byte> authorizeInputBuffer_ = new();
    internal const int InputAuthoredHeader = sizeof(long) * 2;
    Memory<byte> ConstructInputAuthored(long frame, long difference)
    {
        WriteMessageHeader(authorizeInputBuffer_, unreliableHeader_, MessageType.ServerAuthorize);
        authorizeInputBuffer_.Write(frame);
        authorizeInputBuffer_.Write(difference);
        return authorizeInputBuffer_.ExtractAndReplace();
    }

    /// <inheritdoc/>
    public void InputAuthored(long id, long frame, TimeSpan difference)
    {
        /*
         * Packet format:
         * [ Unreliable Message Header ] [ Message Type: byte ] [ Frame: long ] [ Difference: long ]
         */

        long rawDifference = BitConverter.DoubleToInt64Bits(difference.TotalSeconds);
        var message = ConstructInputAuthored(frame, rawDifference);
        transport_.SendUnreliable(message, id);
    }

    // Authoritative Input
    readonly PooledBufferWriter<byte> authInputBuffer_ = new();
    internal const int AuthoritativeInputHeader = sizeof(long) + Bits.NullableLongSize;
    Memory<byte> ConstructAuthoritativeInput<TPayload>(long frame, long? checksum, TPayload payload)
    {
        WriteMessageHeader(authInputBuffer_, reliableHeader_, MessageType.ServerAuthInput);
        authInputBuffer_.Write(frame);
        authInputBuffer_.Write(checksum);
        MemoryPackSerializer.Serialize(authInputBuffer_, payload);
        return authInputBuffer_.ExtractAndReplace();
    }

    /// <inheritdoc/>
    public void SendAuthoritativeInput<TAuthInputPayload>(long frame, long? checksum, TAuthInputPayload payload)
    {
        /*
         * Packet format:
         * [ Reliable Message Header ] [ Message Type: byte ] [ Frame: long ] [ Checksum: long? ] [ Payload: byte[] ]
         */

        var message = ConstructAuthoritativeInput(frame, checksum, payload);
        transport_.SendReliable(message);
    }
}
