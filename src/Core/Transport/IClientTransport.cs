namespace Core.Transport;

// TODO: finish docs (thread safety)

public interface IClientTransport<TIn, TOut> : IClientInTransport<TIn>, IClientOutTransport<TOut>
where TIn : class
where TOut : class
{
    /// <summary>
    /// Starts the client, attempts to connect to the server.
    /// </summary>
    /// <remarks>
    /// May be called only once. All events this client produces will be called only after the client has been started.
    /// </remarks>
    /// <returns>Task which completes when the connection attempt is finished.</returns>
    Task Start();
}

public interface IClientInTransport<TIn>
{
    /// <summary>
    /// Event which is invoked when the client completes..
    /// </summary>
    event Action OnFinish;

    /// <summary>
    /// Event which is invoked when a message from a client is invoked.
    /// </summary>
    event Action<TIn> OnMessage;
}

public interface IClientOutTransport<TOut>
{
    /// <summary>
    /// Sends a message to the server. The server must receive this message, unless the client is terminated.
    /// </summary>
    /// <param name="message">Message to send.</param>
    void SendReliable(TOut message);

    /// <summary>
    /// Sends a message to the server. The server may not receive this message due to packet loss.
    /// </summary>
    /// <param name="message"></param>
    void SendUnreliable(TOut message);

    /// <summary>
    /// Terminate the connection. Some sent messages may not arrive.
    /// </summary>
    void Terminate();
}
