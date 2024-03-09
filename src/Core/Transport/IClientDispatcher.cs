using System;

namespace Core.Transport;


/// <summary>
/// Implements the client-side sending and receiving binary messages.
/// </summary>
public interface IClientDispatcher : IClientSender, IClientReceiver  { }

/// <summary>
/// Allows sending messages of the application protocol to the server.
/// </summary>
/// <remarks>
/// The threading model:
/// Calling different methods concurrently is thread safe. Calling the same method concurrently is not.
/// </remarks>
public interface IClientSender
{
    /// <summary>
    /// Disconnect from the server and terminate the connection (reliable).
    /// </summary>
    /// <remarks>
    /// Some messages might be cancelled some still sent
    /// </remarks>
    void Disconnect();

    /// <summary>
    /// Send the clients input for a particular frame (unreliable).
    /// </summary>
    /// <remarks>
    /// See <see cref="AddInputDelegate"/> for more.
    /// </remarks>
    /// <typeparam name="TInputPayload">The type of the input structure, defined by the application protocol.</typeparam>
    /// <param name="frame">State frame this input shall result in.</param>
    /// <param name="payload">Borrow of the input to be serialized.</param>
    void SendInput<TInputPayload>(long frame, TInputPayload payload);
}

/// <summary>
/// Start message (reliable) informs the client of their unique ID, to distinguish their data from other clients in the state.
/// </summary>
/// <param name="id">The unique ID of this client connection valid for the rest of the server's lifetime. </param>
public delegate void StartDelegate(long id);

/// <summary>
/// Initialization message for the client (reliable). Contains a whole state for a given frame.
/// This way a late-joining client does not have to receive all inputs since the state for frame 0.
/// </summary>
/// <param name="frame">The frame id of the state.</param>
/// <param name="state">Move of the serialized state (pooled from <see cref="T:ArrayPool{byte}.Shared"/>).</param>
public delegate void InitializeDelegate(long frame, Memory<byte> state);

/// <summary>
/// Auth input message (reliable) is sent to the client for each state update after the initial state message.
/// </summary>
/// <remarks>
/// With the initial input message the following input messages form a deterministic log of the game progression i.e. a replay log.
/// The client may at start receive message which precede the initialization message and thus may be safely ignored.
/// </remarks>
/// <param name="frame">The frame of the resulting state update i.e. the state id produced by using this input on previous state.</param>
/// <param name="input">Move of the serialized auth input structure (pooled from <see cref="T:ArrayPool{byte}.Shared"/>).</param>
/// <param name="checksum">Optional checksum for the resulting state.</param>
public delegate void AddAuthInputDelegate(long frame, Memory<byte> input, long? checksum);

/// <summary>
/// Set delay message (unreliable) tells how much ahead a given input of this client was received by the server before the corresponding state update.
/// </summary>
/// <param name="frame">The frame index of the state update.</param>
/// <param name="delay">Amount of time in seconds.</param>
/// <remarks>
/// If given input was not received in time, the value is negative.
/// </remarks>
/// <example>
/// 1. Client A sends their input for state update X.
/// 2. The server receives A.
/// 3. X is executed.
/// 4. Server sends delay with value of T(3) - T(2), where T is time of given event.
/// This allows the client to send messages just before the update hits (makes the experience most responsive).
/// </example>
public delegate void SetDelayDelegate(long frame, double delay);

/// <summary>
/// Notifies about client-targeted messages of the application protocol.
/// </summary>
/// <remarks>
/// Reliable messages are notified in the order they were sent.
/// </remarks>
public interface IClientReceiver
{
    /// <summary>
    /// Notification for the start message.
    /// </summary>
    event StartDelegate OnStart;
    
    /// <summary>
    /// Notification for the initialize message.
    /// </summary>
    event InitializeDelegate OnInitialize;
    
    /// <summary>
    /// Notification for the auth input message.
    /// </summary>
    event AddAuthInputDelegate OnAddAuthInput;
    
    /// <summary>
    /// Notification for the set delay message.
    /// </summary>
    event SetDelayDelegate OnSetDelay;
}
