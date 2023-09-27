using MemoryPack;

namespace Core;

/// <summary>
/// The game state. Holds all required information for the deterministic update.
/// This object must hold the <see cref="MemoryPackableAttribute"/>.
/// </summary>
/// <typeparam name="TC">Type of the client input.</typeparam>
/// <typeparam name="TS">Type of the server input.</typeparam>
public interface IGameState<TC, TS>
    where TC : class, new()
    where TS : class, new()
{
    public UpdateOutput Update(UpdateInput<TC, TS> updateInputs);

    public static abstract double DesiredTickRate { get; }
}
