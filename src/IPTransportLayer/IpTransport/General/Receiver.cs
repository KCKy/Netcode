using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Kcky.GameNewt.Transport.Default;

struct Receiver<TIn>
{
    readonly IReceiveProtocol<TIn> protocol_;
    readonly ILogger logger_ = Log.ForContext<Receiver<TIn>>();

    public Receiver(IReceiveProtocol<TIn> protocol)
    {
        protocol_ = protocol;
    }

    public event Action<TIn>? OnMessage;

    public async Task RunAsync(CancellationToken cancellation)
    {
        try
        {
            while (true)
            {
                TIn message = await protocol_.ReceiveAsync(cancellation);
                OnMessage?.Invoke(message);
            }
        }
        catch (OperationCanceledException)
        {
            logger_.Debug("Receiver was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            logger_.Error(ex, "Receiver failed.");
            throw;
        }
    }
}
