using System.Buffers;

namespace Core.Transport;

public interface IClientSender
{
    void Disconnect();
    void SendInput<TInputPayload>(long frame, TInputPayload payload);
}

public delegate void StartDelegate(long id);
public delegate void InitializeDelegate(long frame, Memory<byte> input);
public delegate void AddAuthInputDelegate(long frame, Memory<byte> input, long? checksum);
public delegate void SetDelayDelegate(double delay);

public interface IClientReceiver
{
    event StartDelegate OnStart;
    event InitializeDelegate OnInitialize;
    event AddAuthInputDelegate OnAddAuthInput;
    event SetDelayDelegate OnSetDelay;
}
