namespace Core.Transport;

public interface IServerSender
{
    void Kick(long id);

    void Initialize<TPayload>(long id, long frame, TPayload payload);

    void InputAuthored(long id, long frame, TimeSpan difference);

    void SendAuthoritativeInput<TPayload>(long frame, long? checksum, TPayload payload);
}

public delegate void AddInputDelegate(long id, long frame, ReadOnlySpan<byte> input);

public interface IServerReceiver
{
    event AddInputDelegate OnAddInput;
    event Action<long> OnAddClient;
    event Action<long> OnRemoveClient;
}
