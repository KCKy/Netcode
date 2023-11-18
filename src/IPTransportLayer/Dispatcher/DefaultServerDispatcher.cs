using Core.Transport;
using System.Buffers;
using Core.Utility;
using MemoryPack;
using Serilog;
using Useful;

namespace DefaultTransport.Dispatcher;

public sealed class DefaultServerDispatcher : IServerSender, IServerReceiver
{
    readonly IServerTransport transport_;

    readonly int unreliableHeader_;
    readonly int reliableHeader_;
    readonly ILogger logger_ = Log.ForContext<DefaultServerDispatcher>();

    public DefaultServerDispatcher(IServerTransport transport)
    {
        transport_ = transport;

        unreliableHeader_ = transport.UnreliableMessageHeader;
        reliableHeader_ = transport.ReliableMessageHeader;

        transport.OnUnreliableMessage += HandleMessage;
        transport.OnReliableMessage += HandleMessage;
    }

    public void Kick(long id) => transport_.Terminate(id);

    public event AddInputDelegate? OnAddInput;
    
    public event Action<long> OnAddClient
    {
        add => transport_.OnClientJoin += value;
        remove => transport_.OnClientJoin -= value;
    }
    
    public event Action<long> OnRemoveClient
    {
        add => transport_.OnClientFinish += value;
        remove => transport_.OnClientFinish -= value;
    }

    void WriteMessageHeader(PooledBufferWriter<byte> writer, int preHeader, MessageType type)
    {
        writer.Skip(preHeader);
        writer.GetSpan(1)[0] = (byte)type;
        writer.Advance(1);
    }
    
    void HandleMessage(long id, Memory<byte> message)
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
            case MessageType.ClientInput:
                HandleClientInput(id, message);
                return;
            default:
                logger_.Error("Received invalid message from client {Id}: {Message} of type {Type}.", id, message, type);
                return;
        }
    }

    void HandleClientInput(long id, Memory<byte> owner)
    {
        var message = owner.Span;
        while (message.Length > sizeof(long) + sizeof(int))
        {
            long frame = message.ReadLong();
            int length = message.ReadInt();

            if (length <= 0)
                break;

            if (message.Length < length)
                break;
            
            OnAddInput?.Invoke(id, frame, message[..length]);

            message = message[length..];
        }

        ArrayPool<byte>.Shared.Return(owner);
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
    public void Initialize<TPayload>(long id, long frame, TPayload payload)
    {
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

    public void InputAuthored(long id, long frame, TimeSpan difference)
    {
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

    public void SendAuthoritativeInput<TAuthInputPayload>(long frame, long? checksum, TAuthInputPayload payload)
    {
        var message = ConstructAuthoritativeInput(frame, checksum, payload);
        transport_.SendReliable(message);
    }
}
