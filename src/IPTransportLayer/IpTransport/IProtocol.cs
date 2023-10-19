namespace DefaultTransport.IpTransport;

interface IProtocol<TIn, in TOut>
{
    ValueTask<TIn> ReadAsync(CancellationToken cancellation);
    ValueTask WriteAsync(TOut data, CancellationToken cancellation);
    Task CloseAsync();
}
