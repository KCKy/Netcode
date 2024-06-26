﻿using System;
using System.Threading.Tasks;

namespace Kcky.GameNewt.Dispatcher;

/// <summary>
/// Implements the server-side sending and receiving binary messages.
/// </summary>
public interface IServerDispatcher : IServerSender, IServerReceiver
{
    /// <summary>
    /// Terminate the dispatcher instance.
    /// All dispatcher operation shall stop soon.
    /// </summary>
    void Terminate();

    /// <summary>
    /// Start the dispatcher instance.
    /// The dispatcher will listen for new clients and start sending/receiving messages.
    /// </summary>
    /// <returns>Task representing the dispatcher lifetime.</returns>
    Task RunAsync();
}

/// <summary>
/// Allows sending messages of the application protocol from the server to clients.
/// </summary>
/// <remarks>
/// The threading model:
/// Calling different methods concurrently is thread safe. Calling the same method concurrently is not.
/// </remarks>
public interface IServerSender
{
    /// <summary>
    /// Kick given client from the server (reliable). They will be forcefully disconnected.
    /// </summary>
    /// <remarks>
    /// Some messages might still be received after kicking (for a brief moment).
    /// ID of already disconnect client will do nothing. Unused ID is undefined behaviour.
    /// </remarks>
    /// <param name="id">ID of the client to kick.</param>
    void Kick(int id);

    /// <summary>
    /// Initialize given client with given serialized state for a frame.
    /// </summary>
    /// <remarks>
    /// It is thread safe to call this and <see cref="SendAuthoritativeInput{TPayload}"/> concurrently but not desirable
    /// as there would be a race condition on the message ordering,
    /// the client could receive input which should be applied to a state which has not yet been received.
    /// <see cref="InitializeDelegate"/> for more.
    /// </remarks>
    /// <typeparam name="TPayload">The type of state structure, defined by the application protocol.</typeparam>
    /// <param name="id">The ID of the client to send to. Invalid ID will do nothing.</param>
    /// <param name="frame">The frame of the state belongs to.</param>
    /// <param name="payload">Read only borrow of the state structure to be serialized.</param>
    void Initialize<TPayload>(int id, long frame, TPayload payload);

    /// <summary>
    /// Authorize that a given input from a client has been received and specify, how ahead it was before the state update.
    /// </summary>
    /// <remarks>
    /// See <see cref="SetDelayDelegate"/> for more.
    /// </remarks>
    /// <param name="id">The id of the client to rec</param>
    /// <param name="frame">The frame the input corresponds to.</param>
    /// <param name="difference">The delay in seconds between receiving the input and the state update.</param>
    void SetDelay(int id, long frame, float difference);

    /// <summary>
    /// Send authoritative input for given state update.
    /// </summary>
    /// <remarks>
    /// See <see cref="AuthoritativeInputDelegate"/> for more.
    /// </remarks>
    /// <typeparam name="TPayload">The type of auth input structure, defined by the application protocol.</typeparam>
    /// <param name="frame">The frame of the state update.</param>
    /// <param name="checksum">An optional checksum of the resulting state.</param>
    /// <param name="payload">>Read only borrow of the input structure to be serialized.</param>
    void SendAuthoritativeInput<TPayload>(long frame, long? checksum, TPayload payload);
}

/// <summary>
/// Message of an input for particular frame from a single client (unreliable).
/// </summary>
/// <remarks>
/// This input is unreliable and therefore can be received whenever and even multiple times.
/// </remarks>
/// <param name="id">The id of client this input was received from.</param>
/// <param name="frame">The frame of the state update the input belongs to.</param>
/// <param name="input">Read only borrow of the serialized input.</param>
public delegate void AddInputDelegate(int id, long frame, ReadOnlyMemory<byte> input);

/// <summary>
/// Notifies about server-targeted messages of the application protocol.
/// </summary>
/// <remarks>
/// Reliable messages are notified in the order they were sent.
/// </remarks>
public interface IServerReceiver
{
    /// <summary>
    /// Notification for the client input message.
    /// </summary>
    event AddInputDelegate OnAddInput;
    
    /// <summary>
    /// Signalled when a client connects, their ID is passed.
    /// </summary>
    event Action<int> OnAddClient;

    /// <summary>
    /// Signalled when a client disconnects (kicked, network failure, by choice), their ID is passed.
    /// </summary>
    event Action<int> OnRemoveClient;
}
