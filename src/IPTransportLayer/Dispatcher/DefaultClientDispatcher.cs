using System.Buffers;
using Core.Transport;
using Core.Utility;
using MemoryPack;
using Serilog;
using Useful;

namespace DefaultTransport.Dispatcher;

/// <summary>
/// Implementation for the client-side part of the default transport protocol (<see cref="IClientSender"/>, <see cref="IClientReceiver"/>)
/// over a <see cref="IClientTransport"/>.
/// </summary>
/// <remarks>
/// For the server side check <see cref="DefaultServerDispatcher"/>.
/// As specified in the protocol the dispatcher keeps all user inputs for states which have not yet been authored and aggregates
/// them in a single message. 
/// </remarks>
public sealed class DefaultClientDispatcher : IClientSender, IClientReceiver
{
    readonly IClientTransport outTransport_;
    
    readonly int unreliableHeader_;
    readonly int unreliableMaxMessage_;

    readonly ILogger logger_ = Log.ForContext<DefaultClientDispatcher>();
    readonly PacketAggregator aggregator_ = new();

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="transport">The transport which shall be used to send and receive packets.</param>
    public DefaultClientDispatcher(IClientTransport transport)
    {
        outTransport_ = transport;

        unreliableHeader_ = transport.UnreliableMessageHeader;
        unreliableMaxMessage_ = transport.UnreliableMessageMaxLength;

        transport.OnUnreliableMessage += HandleMessage;
        transport.OnReliableMessage += HandleMessage;
    }

    /// <inheritdoc/>
    public event StartDelegate? OnStart;
    
    /// <inheritdoc/>
    public event InitializeDelegate? OnInitialize;
    
    /// <inheritdoc/>
    public event AddAuthInputDelegate? OnAddAuthInput;
    
    /// <inheritdoc/>
    public event SetDelayDelegate? OnSetDelay;

    void HandleMessage(Memory<byte> message)
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
            case MessageType.ServerInitialize:
                HandleServerInitialize(message);
                return;
            case MessageType.ServerAuthorize:
                HandleServerAuthorize(message);
                return;
            case MessageType.ServerAuthInput:
                HandleServerAuthInput(message);
                return;
            case MessageType.ClientInput:
            default:
                logger_.Error("Received invalid message from server: {Message} of type {Type}.", message, type);
                ArrayPool<byte>.Shared.Return(message);
                return;
        }
    }

    void HandleServerInitialize(Memory<byte> message)
    {
        const int headerLength = DefaultServerDispatcher.InitializeHeader;

        if (message.Length < headerLength)
        {
            logger_.Error("Received invalid initialization message: {Message}.", message);
            ArrayPool<byte>.Shared.Return(message);
            return;
        }

        var header = message.Span[..headerLength];
        
        long id = header.ReadLong();
        long frame = header.ReadLong();

        var state = message[headerLength..];
        
        OnStart?.Invoke(id);
        OnInitialize?.Invoke(frame, state); // Transfer memory ownership to the client
    }

    void HandleServerAuthorize(Memory<byte> message)
    {
        const int headerLength = DefaultServerDispatcher.InputAuthoredHeader;

        if (message.Length < headerLength)
        {
            logger_.Error("Received invalid authorize message: {Message}.", message);
            ArrayPool<byte>.Shared.Return(message);
            return; // Return owned memory to the pool
        }

        var header = message.Span[..headerLength];
        
        long frame = header.ReadLong();
        long differenceRaw = header.ReadLong();
        
        double difference = BitConverter.Int64BitsToDouble(differenceRaw);

        if (double.IsRealNumber(difference))
        {
            OnSetDelay?.Invoke(difference);

            lock (aggregator_)
                aggregator_.Pop(frame);
        }
        else
        {
            logger_.Error("Authorize message has invalid time difference: {Message} -> {Difference}.", message, difference);
        }

        ArrayPool<byte>.Shared.Return(message); // Return owned memory to the pool
    }

    void HandleServerAuthInput(Memory<byte> message)
    {
        const int headerLength = DefaultServerDispatcher.AuthoritativeInputHeader;

        if (message.Length < headerLength)
        {
            logger_.Error("Received invalid auth input message: {Message}.", message);
            ArrayPool<byte>.Shared.Return(message);
            return; // Return owned memory to the pool
        }

        var header = message.Span[..headerLength];
        long frame = header.ReadLong();
        long? checksum = header.ReadNullableLong();
        var input = message[headerLength..];

        OnAddAuthInput?.Invoke(frame, input, checksum); // Transfer memory ownership to the client
        
        lock (aggregator_)
            aggregator_.Pop(frame);
    }

    /// <inheritdoc/>
    public void Disconnect() => outTransport_.Terminate();
    
    readonly PooledBufferWriter<byte> inputBuffer_ = new();
    internal const int InputStructHeader = sizeof(long) + sizeof(int);

    Memory<byte> ConstructInput<TInputPayload>(long frame, TInputPayload payload)
    {
        /*
         * Input struct format:
         * [ Frame: long ] [ Payload Length: int ] [ SerializedPayload byte[] ]
         */

        inputBuffer_.Write(frame);
        inputBuffer_.Skip(sizeof(int));
        MemoryPackSerializer.Serialize(inputBuffer_, payload);
        var message = inputBuffer_.ExtractAndReplace();
        
        int fullLength = message.Length;
        int payloadLength = fullLength - InputStructHeader;

        Bits.Write(payloadLength, message.Span[sizeof(long)..]);
        return message;
    }

    /// <inheritdoc/>
    public void SendInput<TInputPayload>(long frame, TInputPayload payload)
    {
        var input = ConstructInput(frame, payload);
        int fullLength = input.Length;

        if (fullLength > unreliableMaxMessage_)
            logger_.Error("Client sends input message larger than assured to be deliverable: {Actual} > {Valid}", fullLength, unreliableMaxMessage_);

        /*
         * Packet format:
         * [ Unreliable Message Header ] [ Message Type: byte ] [ Input struct 1 ] ... [ Input struct N ]
         */

        Memory<byte> packet;

        lock (aggregator_)
            packet = aggregator_.AddAndConstruct(input, frame, unreliableHeader_ + sizeof(byte), unreliableMaxMessage_);
        
        packet.Span[unreliableHeader_] = (byte)MessageType.ClientInput;

        outTransport_.SendUnreliable(packet);
    }
}
