using System;
using System.Linq;
using System.Security.Cryptography;
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
    readonly Random random_ = new();

    public PlayerInput GetInput()
    {
        return random_.Next(4) switch
        {
            0 => new(true, false, false, false),
            1 => new(false, true, false, false),
            2 => new(false, false, true, false),
            3 => new(false, false, false, true),
            _ => throw new Exception()
        };
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
