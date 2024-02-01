namespace Core.Client;

interface IPredictManager<TC, TS, TG>
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    /// <summary>
    /// Initialize the predict manager to be able to receive inputs.
    /// </summary>
    /// <remarks>
    /// This shall be called exactly once before <see cref="InformAuthInput"/> or <see cref="Tick"/> is called.
    /// </remarks>
    /// <param name="frame">The index of the state.</param>
    /// <param name="state">The state to initialize with.</param>
    void Init(long frame, TG state);
    
    /// <summary>
    /// Provide authoritative input for given frame update to check for mispredictions.
    /// </summary>
    /// <remarks>
    /// This shell be called only atomically after given auth state update.
    /// </remarks>
    /// <param name="serializedInput">Serialized authoritative input, only borrowed.</param>
    /// <param name="frame">Index of the frame the input belongs to.</param>
    /// <param name="input"><see cref="serializedInput"/> deserialized, ownership given.</param>
    void InformAuthInput(ReadOnlySpan<byte> serializedInput, long frame, UpdateInput<TC, TS> input);
    
    /// <summary>
    /// Stops the predict manager from further management.
    /// </summary>
    /// <remarks>
    /// This method is thread safe.
    /// </remarks>
    void Stop();
    
    /// <summary>
    /// Update the predict state once.
    /// </summary>
    /// <remarks>
    /// This method is thread safe.
    /// </remarks>
    void Tick();

    /// <summary>
    /// The local id of the client.
    /// </summary>
    /// <remarks>
    /// This shall be set exactly once before <see cref="InformAuthInput"/> or <see cref="Tick"/> is called.
    /// </remarks>
    long LocalId { set; }

    /// <summary>
    /// The current frame of predict simulation.
    /// </summary>
    long Frame { get; }
}
