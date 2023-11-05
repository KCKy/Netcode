using MemoryPack;

namespace DefaultTransport.Dispatcher;

public enum MessageType : byte
{
    ServerInitialize = 1,
    ServerAuthorize = 2,
    ServerAuthInput = 3,
    ClientInput = 101
}
