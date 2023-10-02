using MemoryPack;

namespace Core;

/// <summary>
/// Holds information about input from a given client.
/// </summary>
/// <typeparam name="TClientInput">The type of client input.</typeparam>
[MemoryPackable]
public partial struct UpdateClientInfo<TClientInput>
    where TClientInput : class, new()
{
    /// <summary>
    /// Id of the client.
    /// </summary>
    public long Id;

    /// <summary>
    /// The input the client.
    /// </summary>
    public TClientInput Input;

    /// <summary>
    /// Whether client disconnected at the end of the frame update.
    /// </summary>
    public bool Terminated;

    public void Deconstruct(out long id, out TClientInput input, out bool terminated)
    {
        id = Id;
        input = Input;
        terminated = Terminated;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="id">Id of the client.</param>
    /// <param name="input">The input the client.</param>
    /// <param name="terminated">Whether client disconnected at the end of the frame update.</param>
    public UpdateClientInfo(long id, TClientInput input, bool terminated)
    {
        Id = id;
        Input = input;
        Terminated = terminated;
    }
}
