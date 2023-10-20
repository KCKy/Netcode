using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace DefaultTransport.IpTransport
{
    struct Receiver<TProtocol, TIn>
        where TProtocol : IReceiveProtocol<TIn>
    {
        readonly TProtocol protocol_;
        readonly ILogger logger_ = Log.ForContext<Receiver<TProtocol, TIn>>();

        public Receiver(TProtocol protocol)
        {
            protocol_ = protocol;
        }

        public event Action<TIn>? OnMessage;

        public async Task RunAsync(CancellationToken cancellation)
        {
            while (true)
            {
                TIn message = await protocol_.ReceiveAsync(cancellation);
                OnMessage?.Invoke(message);
            }
        }
    }
}
