using System;
using MemoryPack;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using FrameworkTest;

[MemoryPackable]
public partial class GameState : IGameState<PlayerInput, ServerInput>
{
    public long Tick = -1;

    public Dictionary<long, Vector2> Positions = new();

    public (long Id, PlayerInput Input, bool terminated)[] Inputs = Array.Empty<(long, PlayerInput, bool)>();

    public UpdateOutput Update(in Input<PlayerInput, ServerInput> inputs)
    {
        Tick++;

        Inputs = inputs.PlayerInputs; // TODO: make sure this does not break anything

        foreach ((long id, PlayerInput input, bool terminated) in inputs.PlayerInputs)
        {
            if (terminated)
            {
                Positions.Remove(id);
                continue;
            }

            int dx = (input.Right ? 1 : 0) + (input.Left ? -1 : 0);
            int dy = (input.Down ? 1 : 0) + (input.Up ? -1 : 0);

            Vector2 d = new(dx, dy);

            if (!Positions.TryGetValue(id, out var pos))
            {
                Positions.Add(id, d);
                continue;
            }

            Positions[id] = pos + d;
        }

        return new();
    }

    public static float DesiredTickRate => 0.1f;
}
