using Serilog;

namespace DefaultTransport.IpTransport
{
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
            catch (Exception ex)
            {
                logger_.Error(ex, "Receiver failed.");
                throw;
            }
        }
    }
}
