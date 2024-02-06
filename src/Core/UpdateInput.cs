using MemoryPack;

namespace Core;

/// <summary>
/// Holds all input information for a single state update.
/// </summary>
/// <typeparam name="TClientInput">Type of the client input.</typeparam>
/// <typeparam name="TServerInput">Type of the server input.</typeparam>
[MemoryPackable]
public partial struct UpdateInput<TClientInput, TServerInput>
    where TServerInput : class, new()
    where TClientInput : class, new()
{
    /// <summary>
    /// The input of the server.
    /// </summary>
    public TServerInput ServerInput;
    
    /// <summary>
    /// List for all connected clients containing their inputs.
    /// </summary>
    public Memory<UpdateClientInfo<TClientInput>> ClientInput;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="clientInput">Move of the collection of client inputs.</param>
    /// <param name="serverInput">Move of the server input.</param>
    [MemoryPackConstructor]
    public UpdateInput(Memory<UpdateClientInfo<TClientInput>> clientInput, TServerInput serverInput)
    {
        ServerInput = serverInput;
        ClientInput = clientInput;
    }

    /// <summary>
    /// Constructor. Creates a default empty input.
    /// </summary>
    public UpdateInput()
    {
        ServerInput = new();
        ClientInput = Memory<UpdateClientInfo<TClientInput>>.Empty;
    }

    /// <summary>
    /// Default empty input.
    /// </summary>
    public static UpdateInput<TClientInput, TServerInput> Empty => new();
}
