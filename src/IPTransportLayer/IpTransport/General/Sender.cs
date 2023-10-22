using Core.Server;
using Serilog;

namespace DefaultTransport.IpTransport
{
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
}
