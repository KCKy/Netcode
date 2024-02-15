using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Useful;

namespace DefaultTransport.IpTransport;

/// <summary>
/// Takes messages from a <typeparamref name="TMessages"/> and sends them over a <see cref="ISendProtocol{TOut}"/>.
/// </summary>
/// <typeparam name="TMessages">The type of the collection of messages.</typeparam>
/// <typeparam name="TMessage">The type of the message.</typeparam>
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
        try
        {
            while (true)
            {
                TMessage message = await messages_.GetAsync(cancellation);
                logger_.Verbose("Got message {Payload} to send to a remote.", message);
                await protocol_.SendAsync(message, cancellation);
            }
        }
        catch (OperationCanceledException)
        {
            logger_.Debug("Sender was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            logger_.Error(ex, "Sender failed.");
            throw;
        }
    }
}
