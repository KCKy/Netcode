namespace DefaultTransport.Dispatcher;

/// <summary>
/// Message types of the default transport protocol, backed by the single byte message ID.
/// </summary>
public enum MessageType : byte
{
    /// <summary>
    /// Corresponds to <see cref="DefaultServerDispatcher.Initialize{T}"/>.
    /// </summary>
    ServerInitialize = 1,
    
    /// <summary>
    /// Corresponds to <see cref="DefaultServerDispatcher.InputAuthored"/>.
    /// </summary>
    ServerAuthorize = 2,

    /// <summary>
    /// Corresponds to <see cref="DefaultServerDispatcher.SendAuthoritativeInput{T}"/>.
    /// </summary>
    ServerAuthInput = 3,

    /// <summary>
    /// Corresponds to <see cref="DefaultClientDispatcher.SendInput{T}"/>
    /// </summary>
    ClientInput = 101
}
