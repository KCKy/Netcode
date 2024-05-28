using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Kcky.GameNewt.Transport.Default;

/// <summary>
/// Takes messages from a <typeparamref name="TMessages"/> and sends them over a <see cref="ISendProtocol{TOut}"/>.
/// </summary>
/// <typeparam name="TMessages">The type of the collection of messages.</typeparam>
/// <typeparam name="TMessage">The type of the message.</typeparam>
struct Sender<TMessages, TMessage>(ISendProtocol<TMessage> protocol, TMessages messages, ILoggerFactory loggerFactory)
    where TMessages : IPendingMessages<TMessage>
{
    readonly TMessages messages_ = messages;
    readonly ILogger logger_ = loggerFactory.CreateLogger<Sender<TMessages, TMessage>>();

    public async Task RunAsync(CancellationToken cancellation)
    {
        try
        {
            while (true)
            {
                TMessage message = await messages_.GetAsync(cancellation);
                logger_.LogTrace("Got message to send to a remote.");
                await protocol.SendAsync(message, cancellation);
            }
        }
        catch (OperationCanceledException)
        {
            logger_.LogDebug("Sender was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            logger_.LogError(ex, "Sender failed.");
            throw;
        }
    }
}
