using System.Buffers;
using Core.Transport;
using Core.Utility;
using MemoryPack;
using Serilog;
using Useful;

namespace DefaultTransport.Dispatcher;

public sealed class DefaultClientDispatcher : IClientSender, IClientReceiver
{
    readonly IClientTransport outTransport_;
    
    readonly int unreliableHeader_;
    readonly int unreliableMaxMessage_;

    readonly ILogger logger_ = Log.ForContext<DefaultClientDispatcher>();
    PacketAggregator aggregator_ = new();

    public DefaultClientDispatcher(IClientTransport transport)
    {
        outTransport_ = transport;

        unreliableHeader_ = transport.UnreliableMessageHeader;
        unreliableMaxMessage_ = transport.UnreliableMessageMaxLength;

        transport.OnUnreliableMessage += HandleMessage;
        transport.OnReliableMessage += HandleMessage;
    }

    public event StartDelegate? OnStart;
    public event InitializeDelegate? OnInitialize;
    public event AddAuthInputDelegate? OnAddAuthInput;
    public event SetDelayDelegate? OnSetDelay;

    void HandleMessage(Memory<byte> message)
    {
        if (message.IsEmpty)
        {
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
                return;
        }
    }

    void HandleServerInitialize(Memory<byte> binary)
    {
        const int length = DefaultServerDispatcher.InputAuthoredHeader;

        if (binary.Length < length)
            return;

        var header = binary.Span[..length];
        
        long id = header.ReadLong();
        long frame = header.ReadLong();
        var state = binary[length..];
        
        OnStart?.Invoke(id);
        OnInitialize?.Invoke(frame, state);
    }

    void HandleServerAuthorize(Memory<byte> binary)
    {
        const int length = DefaultServerDispatcher.InputAuthoredHeader;

        if (binary.Length >= length)
        {
            var header = binary.Span[..length];
        
            long frame = header.ReadLong();
            long differenceRaw = header.ReadLong();
        
            double difference = BitConverter.Int64BitsToDouble(differenceRaw);
            OnSetDelay?.Invoke(difference);
            aggregator_.Pop(frame);
        }

        ArrayPool<byte>.Shared.Return(binary);
    }

    void HandleServerAuthInput(Memory<byte> binary)
    {
        const int length = DefaultServerDispatcher.AuthoritativeInputHeader;

        if (binary.Length < length)
            return;

        var header = binary.Span[..length];
        long frame = header.ReadLong();
        long? checksum = header.ReadNullableLong();
        var input = binary[length..];

        OnAddAuthInput?.Invoke(frame, input, checksum);
        aggregator_.Pop(frame);
    }

    public void Disconnect() => outTransport_.Terminate();
    
    readonly PooledBufferWriter<byte> inputBuffer_ = new();
    internal const int InputHeader = sizeof(long) + sizeof(int);
    public void SendInput<TInputPayload>(long frame, TInputPayload payload)
    {
        inputBuffer_.Write(frame);
        inputBuffer_.Skip(sizeof(int));
        MemoryPackSerializer.Serialize(inputBuffer_, payload);
        var message = inputBuffer_.ExtractAndReplace();
        
        int fullLength = message.Length;
        int payloadLength = fullLength - sizeof(long) - sizeof(int);

        Bits.Write(payloadLength, message.Span[sizeof(long)..]);

        if (fullLength > unreliableMaxMessage_)
            logger_.Error("Client sends input message larger than assured to be deliverable: {Actual} > {Valid}", fullLength, unreliableMaxMessage_);

        var messageAggregate = aggregator_.AddAndConstruct(message, frame, unreliableHeader_ + sizeof(byte), unreliableMaxMessage_);
        messageAggregate.Span[unreliableHeader_] = (byte)MessageType.ClientInput;

        outTransport_.SendUnreliable(messageAggregate);
    }
}
