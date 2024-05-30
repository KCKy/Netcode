using Kcky.GameNewt;
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Tester.UpdateFrequency;

[MemoryPackable]
partial class ClientInput;

[MemoryPackable]
partial class ServerInput;

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        return UpdateOutput.Empty;
    }

    public static float DesiredTickRate => Program.TickRate;
}
