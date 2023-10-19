namespace DefaultTransport.IpTransport;

class OtherSideEndedException : ApplicationException
{
    public OtherSideEndedException() { }
    public OtherSideEndedException(string message) : base(message) { }
    public OtherSideEndedException(string message, Exception inner) : base(message, inner) { }
}

public class TimedOutException : ApplicationException
{
    public TimedOutException() { }
    public TimedOutException(string message) : base(message) { }
    public TimedOutException(string message, Exception inner) : base(message, inner) { }
}
