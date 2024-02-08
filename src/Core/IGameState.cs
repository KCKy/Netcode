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
    /// <summary>
    /// The deterministic update method of the game state.
    /// </summary>
    /// <param name="updateInputs">The inputs for the update. The result of this update on the state and the return value shall be purely determined by this value.</param>
    /// <returns>The result of the state update. Used for kicking clients and determining the end of the game simulation.</returns>
    UpdateOutput Update(UpdateInput<TC, TS> updateInputs);

    /// <summary>
    /// Number of ticks per second the game is designed to run it. Shall be a constant value across all platforms.
    /// </summary>
    static abstract double DesiredTickRate { get; }
}
