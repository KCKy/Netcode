using Kcky.GameNewt;
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Tester.InputPropagation;

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
    public SortedDictionary<int, List<int>> ClientIdToReceivedInputs = new();

    public List<int> ServerInputs = new();

    public long Frame = -1;
    public long TotalFrames = long.MaxValue;

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        foreach ((int id, ClientInput input, _) in updateInputs.ClientInputInfos.Span)
        {
            if (input.Value < 0)
                continue;

            if (!ClientIdToReceivedInputs.TryGetValue(id, out List<int>? received))
            {
                received = new();
                ClientIdToReceivedInputs.Add(id, received);
            }

            received.Add(input.Value);
        }

        if (updateInputs.ServerInput.Value >= 0)
            ServerInputs.Add(updateInputs.ServerInput.Value);

        Frame++;

        return Frame == TotalFrames ? UpdateOutput.Terminate : UpdateOutput.Empty;
    }

    public static float DesiredTickRate => Program.TickRate;
}
