using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Transport.Default;

struct Receiver<TIn>(IReceiveProtocol<TIn> protocol, ILoggerFactory loggerFactory)
{
    readonly ILogger logger_ = loggerFactory.CreateLogger<Receiver<TIn>>();
    public event Action<TIn>? OnMessage;

    public async Task RunAsync(CancellationToken cancellation)
    {
        try
        {
            while (true)
            {
                TIn message = await protocol.ReceiveAsync(cancellation);
                OnMessage?.Invoke(message);
            }
        }
        catch (OperationCanceledException)
        {
            logger_.LogDebug("Receiver was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            logger_.LogDebug(ex, "Receiver failed.");
            throw;
        }
    }
}
