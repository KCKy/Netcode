using MemoryPack;

namespace Core;

/// <summary>
/// Holds information about input from given client.
/// </summary>
/// <typeparam name="TPlayerInput"></typeparam>
[MemoryPackable]
public partial struct UpdateClientInfo<TPlayerInput>
{
    /// <summary>
    /// Id of the client.
    /// </summary>
    public long Id;

    /// <summary>
    /// The input the client.
    /// </summary>
    public TPlayerInput Input;

    /// <summary>
    /// Whether client disconnected at the end of the frame update.
    /// </summary>
    public bool Terminated;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="id">Id of the client.</param>
    /// <param name="input">The input the client.</param>
    /// <param name="terminated">Whether client disconnected at the end of the frame update.</param>
    public UpdateClientInfo(long id, TPlayerInput input, bool terminated)
    {
        Id = id;
        Input = input;
        Terminated = terminated;
    }
}

/// <summary>
/// Holds all input information for a single state update.
/// </summary>
/// <typeparam name="TPlayerInput">Type of the client input.</typeparam>
/// <typeparam name="TServerInput">Type of the server input.</typeparam>
[MemoryPackable]
public partial struct UpdateInput<TPlayerInput, TServerInput>
{
    /// <summary>
    /// The input of the server.
    /// </summary>
    public TServerInput ServerInput;
    
    /// <summary>
    /// List for all connected players containing their inputs.
    /// </summary>
    public Memory<UpdateClientInfo<TPlayerInput>> ClientInput;

    public UpdateInput(Memory<UpdateClientInfo<TPlayerInput>> clientInput, TServerInput serverInput)
    {
        ServerInput = serverInput;
        ClientInput = clientInput;
    }
}

/// <summary>
/// Result of a single state update.
/// Provides a way for the game code to manage the server.
/// </summary>
public struct UpdateOutput
{
    /// <summary>
    /// All ids of clients whose connection will be terminated after this frame update.
    /// </summary>
    public long[]? ClientsToTerminate;

    /// <summary>
    /// Whether this frame update was the last and the server shall stop.
    /// </summary>
    public bool ShallStop;

    /// <summary>
    /// Default constructor. Creates an instance representing nothing.
    /// </summary>
    public UpdateOutput()
    {
        ClientsToTerminate = null;
        ShallStop = false;
    }

    public static UpdateOutput Empty = new();
}

/// <summary>
/// The game state. Holds all required information for the deterministic update.
/// This object must hold the <see cref="MemoryPackableAttribute"/>.
/// </summary>
/// <typeparam name="TPlayerInput">Type of the client input.</typeparam>
/// <typeparam name="TServerInput">Type of the server input.</typeparam>
public interface IGameState<TPlayerInput, TServerInput, TUpdateInfo>
{
    public UpdateOutput Update(UpdateInput<TPlayerInput, TServerInput> updateInputs, ref TUpdateInfo updateInfo);

    public static abstract double DesiredTickRate { get; }
}
