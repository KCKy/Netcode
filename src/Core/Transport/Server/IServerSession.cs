namespace Core.Transport;

public enum ClientFinishReason
{
    Unknown = 0,
    Disconnect,
    Kicked,
    NetworkError,
    OsError,
    Corruption
}

public interface IServerSession
{
    void AddClient(long id);

    void AddInput(long id, long frame, Memory<byte> input);

    void FinishClient(long id);
}
