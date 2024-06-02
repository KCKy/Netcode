using Kcky.GameNewt;
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Tester.Stress;

[MemoryPackable]
partial class ClientInput
{
    public Memory<byte> Data = new byte[256];
}

[MemoryPackable]
partial class ServerInput
{
    public Memory<byte> Data = new byte[256];
}

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public Memory<byte> Data = new byte[16 * 1024 * 1024];

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        foreach (ref byte b in Data.Span)
            b++;

        return UpdateOutput.Empty;
    }

    public static float DesiredTickRate => Program.TickRate;
}
