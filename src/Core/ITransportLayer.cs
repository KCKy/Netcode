namespace Core;

public interface ITransport<TInputMessage, TOutputMessage> : IDisposable
{
    public void SendMessageReliable(TOutputMessage message);

    public void SendMessageUnreliable(TOutputMessage payload);
    
    public Task Run();

    public event Action<TInputMessage> OnReceivedMessage;
}
 