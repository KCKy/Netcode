using MemoryPack;
using System.Collections.Generic;
using System.Linq;

namespace FrameworkTest;

[MemoryPackable]
public readonly partial record struct Input<TPlayerInput, TServerInput>
    (TServerInput ServerInput, (long Id, TPlayerInput Input, bool Terminated)[] PlayerInputs);

// TODO: this structure is kinda janky

public readonly record struct UpdateOutput(IEnumerable<long> ClientsToTerminate, bool ShallStop)
{
    public UpdateOutput() : this(Enumerable.Empty<long>(), false) { }
}

// TODO: output can be used to leak the game state.

public interface IGameState<TPlayerInput, TServerInput>
{
    public UpdateOutput Update(in Input<TPlayerInput, TServerInput> inputs);

    public static abstract float DesiredTickRate { get; } // TODO: configurable tick rate
}
