using Core.Extensions;
using Serilog;

namespace DefaultTransport.IpTransport;

sealed class Transceiver<TProtocol, TMessages, TIn, TOut>
    where TMessages : IPendingMessages<TOut>
    where TProtocol : IProtocol<TIn, TOut>
{
    readonly TProtocol protocol_;
    readonly TMessages messages_;

    readonly ILogger logger_ = Log.ForContext<Transceiver<TProtocol, TMessages, TIn, TOut>>();

    public Transceiver(TProtocol protocol, TMessages messages)
    {
        protocol_ = protocol;
        messages_ = messages;
    }

    public event Action<TIn>? OnMessage;

    async Task ReadAsync(CancellationToken cancellation)
    {
        while (true)
        {
            TIn message = await protocol_.ReadAsync(cancellation);
            OnMessage?.Invoke(message);
        }
    }

    async Task WriteAsync(CancellationToken cancellation)
    {
        while (true)
        {
            TOut message = await messages_.GetAsync(cancellation);
            logger_.Verbose("Got message {Payload} to send to a remote.", message);
            await protocol_.WriteAsync(message, cancellation);
        }
    }

    public async Task Run(CancellationToken cancellation)
    {
        Task read = ReadAsync(cancellation);
        Task write = WriteAsync(cancellation);

        try
        {
            Task first = await Task.WhenAny(read, write);
            await first;
        }
        finally
        {
            logger_.Debug("Finishing transceiver run loop.");
            await protocol_.CloseAsync();
        }
    }
}
