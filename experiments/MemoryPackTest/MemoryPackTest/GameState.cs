using MemoryPack;
using System.Collections.Generic;

[MemoryPackable]
public readonly partial record struct Input<TPlayerInput, TServerInput>
    (TServerInput ServerInput, (long Id, TPlayerInput Input, bool Terminated)[] PlayerInputs);

// TODO: this structure is kinda janky

public readonly record struct UpdateOutput<TOutput>
    (IEnumerable<long> ClientsToTerminate, bool ShallStop, TOutput CustomOutput);

// TODO: output can be used to leak the game state.

public interface IGameState<TPlayerInput, TServerInput, TServerOutput>
{
    public UpdateOutput<TServerOutput> Update(in Input<TPlayerInput, TServerInput> inputs);

    public static abstract int DesiredTickRate { get; } // TODO: configurable tick rate
}

