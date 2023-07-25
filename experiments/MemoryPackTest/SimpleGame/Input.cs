using System;
using System.Linq;
using FrameworkTest;
using MemoryPack;

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
    static int globalDir_ = 0;

    readonly int dir_ = globalDir_++ % 8;

    public PlayerInput GetInput(long frame)
    {
        PlayerInput ret = dir_ switch
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
    public Input<PlayerInput, ServerInput> PredictInput(GameState state)
    {
        // Just copy the player inputs
        var inputEnum = from input in state.Inputs where !input.terminated select input;
        return new(new(), inputEnum.ToArray());
    }
}
