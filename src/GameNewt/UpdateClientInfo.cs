﻿using MemoryPack;

namespace Kcky.GameNewt;

/// <summary>
/// Holds information about input from a given client.
/// </summary>
/// <typeparam name="TClientInput">The type of client input.</typeparam>
[MemoryPackable]
public partial struct UpdateClientInfo<TClientInput>
    where TClientInput : class, new()
{
    /// <summary>
    /// ID of the client.
    /// </summary>
    public int Id;

    /// <summary>
    /// The input the client.
    /// </summary>
    public TClientInput Input;

    /// <summary>
    /// Whether client disconnected at the end of the frame update.
    /// </summary>
    public bool Terminated;

    /// <summary>
    /// Deconstructor.
    /// </summary>
    /// <param name="id">ID of the client.</param>
    /// <param name="input">The input the client.</param>
    /// <param name="terminated">Whether client disconnected at the end of the frame update.</param>
    public readonly void Deconstruct(out int id, out TClientInput input, out bool terminated)
    {
        id = Id;
        input = Input;
        terminated = Terminated;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="id">ID of the client.</param>
    /// <param name="input">The input the client.</param>
    /// <param name="terminated">Whether client disconnected at the end of the frame update.</param>
    public UpdateClientInfo(int id, TClientInput input, bool terminated)
    {
        Id = id;
        Input = input;
        Terminated = terminated;
    }
}
