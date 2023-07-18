using MemoryPack;
using System.Collections.Generic;
using System.Numerics;

[MemoryPackable]
public partial class GameState : IGameState<PlayerInput, ServerInput, UpdateOutput>
{
    public Dictionary<long, Vector2> Positions = new();

    public UpdateOutput<UpdateOutput> Update(in Input<PlayerInput, ServerInput> inputs)
    {
        foreach ((long id, PlayerInput input, bool terminated) in inputs.PlayerInputs)
        {
            if (terminated)
            {
                Positions.Remove(id);
                continue;
            }

            int dx = (input.Right ? 1 : -1) + (input.Left ? -1 : 1);
            int dy = (input.Down ? 1 : -1) + (input.Up ? -1 : 1);

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

    public static int DesiredTickRate => 20;
}

public struct UpdateOutput { }
