using System;

namespace Kcky.GameNewt.Transport.Default;

/// <summary>
/// Thrown when a connection is being closed due to the ender side ending the connection.
/// </summary>
class OtherSideEndedException : ApplicationException
{
    public OtherSideEndedException() { }
    public OtherSideEndedException(string message) : base(message) { }
    public OtherSideEndedException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a <see cref="IpClientTransport"/> times out.
/// </summary>
public class TimedOutException : ApplicationException
{
    /// <inheritdoc/>
    public TimedOutException() { }
    
    /// <inheritdoc/>
    public TimedOutException(string message) : base(message) { }
    
    /// <inheritdoc/>
    public TimedOutException(string message, Exception inner) : base(message, inner) { }
}
