using Microsoft.Extensions.Logging;
using Kcky.GameNewt;
using MemoryPack;

namespace Tester.Kicking;

[MemoryPackable]
partial class ClientInput;

[MemoryPackable]
partial class ServerInput;

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public long Frame = -1;
    public long TotalFrames = long.MaxValue;

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        List<int> toTerminate = new();
        foreach (UpdateClientInfo<ClientInput> x in updateInputs.ClientInputInfos.Span)
            toTerminate.Add(x.Id);
        Frame++;

        if (Frame >= TotalFrames)
            return UpdateOutput.Terminate;

        return new()
        {
            ClientsToTerminate = toTerminate.ToArray(),
        };
    }

    public static float DesiredTickRate => Program.TickRate;
}
