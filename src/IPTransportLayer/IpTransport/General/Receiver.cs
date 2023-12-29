using System.Net.Sockets;
using Serilog;

namespace DefaultTransport.IpTransport
{
    struct Receiver<TIn>
    {
        readonly IReceiveProtocol<TIn> protocol_;
        readonly ILogger Logger = Log.ForContext<Receiver<TIn>>();

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
                Logger.Debug("Receiver was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Receiver failed.");
                throw;
            }
        }
    }
}
