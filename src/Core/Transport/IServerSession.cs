namespace Core.Transport;

public interface IServerSession
{
    void AddClient(long id);
    void AddInput(long id, long frame, Memory<byte> input);
    void FinishClient(long id);
}
