namespace Core.Transport;

public interface IClientSession
{
    void Start(long id);
    void Initialize(long frame, Memory<byte> input);
    void AddAuthoritativeInput(long frame, Memory<byte> input, long? checksum);
    void Finish();
    void SetDelay(double delay);
    void SetRoundTrip(double roundtrip);
}
