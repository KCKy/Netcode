namespace Core.Transport;

public interface IServerTransport<TIn, TOut> : IServerInTransport<TIn>, IServerOutTransport<TOut>
    where TIn : class
    where TOut : class
{
    Task Start();
}

public interface IServerInTransport<TIn>
{
    event Action<long, TIn> OnMessage;
    event Action<long> OnClientJoin;
    event Action<long> OnClientFinish;
}

public interface IServerOutTransport<TOut>
{
    void SendReliable(TOut message);
    void SendUnreliable(TOut message);
    void SendReliable(TOut message, long id);
    void SendUnreliable(TOut message, long id);
    void Terminate(long id);
}
