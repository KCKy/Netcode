using System.Buffers;
using Serilog;
using Useful;

namespace DefaultTransport.IpTransport;

struct Sender<TMessages, TMessage>
    where TMessages : IPendingMessages<TMessage>
{
    readonly ISendProtocol<TMessage> protocol_;
    readonly TMessages messages_;

    readonly ILogger logger_ = Log.ForContext<Sender<TMessages, TMessage>>();

    public Sender(ISendProtocol<TMessage> protocol, TMessages messages)
    {
        protocol_ = protocol;
        messages_ = messages;
    }

    public async Task RunAsync(CancellationToken cancellation)
    {
        while (true)
        {
            TMessage message = await messages_.GetAsync(cancellation);
            logger_.Verbose("Got message {Payload} to send to a remote.", message);
            await protocol_.SendAsync(message, cancellation);
        }
    }
}

struct MemorySender<TMessages>
    where TMessages : IPendingMessages<Memory<byte>>
{
    readonly ISendProtocol<Memory<byte>> protocol_;
    readonly TMessages messages_;

    readonly ILogger logger_ = Log.ForContext<MemorySender<TMessages>>();

    public MemorySender(ISendProtocol<Memory<byte>> protocol, TMessages messages)
    {
        protocol_ = protocol;
        messages_ = messages;
    }

    public async Task RunAsync(CancellationToken cancellation)
    {
        while (true)
        {
            var message = await messages_.GetAsync(cancellation);
            logger_.Verbose("Got message {Payload} to send to a remote.", message);
            await protocol_.SendAsync(message, cancellation);
            ArrayPool<byte>.Shared.Return(message);
        }
    }
}
