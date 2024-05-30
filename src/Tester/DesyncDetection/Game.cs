using Kcky.GameNewt;
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Tester.DesyncDetection;

[MemoryPackable]
partial class ClientInput;

[MemoryPackable]
partial class ServerInput;

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public int MyValue = 0;
    public static int NotPartOfState = 0;

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        MyValue += NotPartOfState; // This is meant to be wrong.
        return UpdateOutput.Empty;
    }

    public static float DesiredTickRate => Program.TickRate;
}
