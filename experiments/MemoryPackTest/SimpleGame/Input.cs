using System;
using System.Linq;
using FrameworkTest;
using MemoryPack;

namespace SimpleGame;

[MemoryPackable]
public partial record struct PlayerInput (bool Up, bool Down, bool Left, bool Right);

[MemoryPackable]
public partial record struct ServerInput { }

public sealed class ServerInputProvider : IServerInputProvider<ServerInput>
{
    public ServerInput GetInput() => new();
    public ServerInput GetInitialInput() => new();
}

public sealed class PlayerInputProvider : IPlayerInputProvider<PlayerInput>
{
    readonly Random random_ = new(DateTime.UtcNow.Nanosecond);

    public int Dir { get; init; } = 0;

    public PlayerInput GetInput(long frame)
    {
        int dir = Dir;

        if (random_.NextSingle() < 0.001)
            dir = random_.Next(0, 8);

        PlayerInput ret = dir switch
        {
            0 => new(true, false, false, false),
            1 => new(false, true, false, false),
            2 => new(false, false, true, false),
            3 => new(false, false, false, true),
            4 => new(true, false, true, false),
            5 => new(true, false, false, true),
            6 => new(false, true, true, false),
            7 => new(false, true, false, true),
            _ => throw new Exception()
        };

        return ret;
    }
}

public sealed class InputPredictor : IInputPredictor<PlayerInput, ServerInput, GameState>
{
    public Input<PlayerInput, ServerInput> PredictInput()
    {
        return new Input<PlayerInput, ServerInput>(new(), Array.Empty<(long, PlayerInput, bool)>());
    }

    public Input<PlayerInput, ServerInput> PredictInput(in Input<PlayerInput, ServerInput> input)
    {
        var inputEnum = from i in input.PlayerInputs where !i.Terminated select i;
        return new Input<PlayerInput, ServerInput>(new(), inputEnum.ToArray());
    }
}
