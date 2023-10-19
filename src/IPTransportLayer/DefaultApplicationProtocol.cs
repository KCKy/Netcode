using System.Buffers;
using Core.Extensions;

namespace DefaultTransport
{ 
    public enum Messages : byte
    {
        ClientInput = 1,
        AuthoritativeFrame,
        InputDelay,
        Initialize
    }

    static class DefaultApplicationProtocol
    {
        public const int HeaderSize = sizeof(Messages);

        public static Memory<byte> PreparePacket(Messages messageType, int payloadLength)
        {
            var input = ArrayPool<byte>.Shared.RentMemory(HeaderSize + payloadLength);
            input.Span[0] = (byte)messageType;
            return input;
        }
    }
}
