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

    [MemoryPackConstructor]
    public UpdateInput(Memory<UpdateClientInfo<TClientInput>> clientInput, TServerInput serverInput)
    {
        ServerInput = serverInput;
        ClientInput = clientInput;
    }

    public UpdateInput()
    {
        ServerInput = new();
        ClientInput = Memory<UpdateClientInfo<TClientInput>>.Empty;
    }

    public static UpdateInput<TClientInput, TServerInput> Empty => new();
}
