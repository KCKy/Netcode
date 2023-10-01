namespace Core.Transport;

public interface IServerDispatcher
{
    void Kick(long id);

    void Initialize(long id, long frame, Memory<byte> state);

    void InputAuthored(long id, long frame, TimeSpan difference);

    void SendAuthoritativeInput(long frame, Memory<byte> input, long? checksum);
}
