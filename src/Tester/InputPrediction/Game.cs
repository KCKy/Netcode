using Kcky.GameNewt;
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Tester.InputPrediction;

[MemoryPackable]
partial class ClientInput
{
    public int Value = int.MinValue;
}

[MemoryPackable]
partial class ServerInput
{
    public int Value = int.MinValue;
}

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public long Frame = -1;

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        Frame++;
        return UpdateOutput.Empty;
    }

    public static float DesiredTickRate => Program.TickRate;
}
