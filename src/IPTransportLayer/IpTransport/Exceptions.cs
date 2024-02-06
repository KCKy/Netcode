namespace DefaultTransport.IpTransport;

class OtherSideEndedException : ApplicationException
{
    public OtherSideEndedException() { }
    public OtherSideEndedException(string message) : base(message) { }
    public OtherSideEndedException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when 
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
