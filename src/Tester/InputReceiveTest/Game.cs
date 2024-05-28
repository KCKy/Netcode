﻿using Kcky.GameNewt;
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Tester.InputReceiveTest;

[MemoryPackable]
partial class ClientInput
{
    public int Value = int.MinValue;
}

[MemoryPackable]
partial class ServerInput;

[MemoryPackable]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public SortedDictionary<int, List<int>> ClientIdToReceivedInputs = new();

    public long Frame = 0;
    public long TotalFrames = long.MaxValue;

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        foreach ((int id, ClientInput input, _) in updateInputs.ClientInputInfos.Span)
        {
            if (input.Value == int.MinValue)
                continue;

            if (!ClientIdToReceivedInputs.TryGetValue(id, out List<int>? received))
            {
                received = new();
                ClientIdToReceivedInputs.Add(id, received);
            }

            received.Add(input.Value);
        }

        Frame++;

        if (Frame == TotalFrames)
        {
            return new UpdateOutput()
            {
                ShallStop = true
            };
        }

        return UpdateOutput.Empty;
    }

    public static float DesiredTickRate => Program.TickRate;
}
