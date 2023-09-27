namespace Core.Transport;

public interface IClientDispatcher
{
    void Disconnect();

    void SendInput(long frame, Memory<byte> input);
}
