using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.DataStructures;

/// <summary>
/// Event describing the authorization of a given client's input.
/// If <paramref name="difference"/> is negative that means given input was not received in time and the server ignored the late input,
/// positive value means the input was received in-time to be accounted for in the frame update.
/// </summary>
/// <param name="id">The ID of the client the input belongs to.</param>
/// <param name="frame">The frame index of the frame the input is for.</param>
/// <param name="difference">The difference of the corresponding frame update time and the input receive time.</param>
delegate void InputAuthoredDelegate(int id, long frame, TimeSpan difference);

/// <summary>
/// Receives all client input to the server. Constructs authoritative client update inputs <see cref="UpdateClientInfo{TClientInput}"/>.
/// Authorizes all received inputs and raises <see cref="inputAuthored_"/> informing whether inputs are being received on time.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
sealed class ClientInputQueue<TClientInput>
where TClientInput : class, new()
{
    sealed class SingleClientQueue(PredictClientInputDelegate<TClientInput> predictClientInput)
    {
        public long LastAuthorizedInput = long.MinValue;
        readonly Dictionary<long, (TClientInput input, long timestamp)> frameToInput_ = new();
        TClientInput previousInput_ = new();

        public bool TryAdd(long frame, TClientInput input, long timestamp) => frameToInput_.TryAdd(frame, (input, timestamp));

        public long? WriteUpdateInfo(long frame, ref UpdateClientInfo<TClientInput> info)
        {
            long? time;

            if (frameToInput_.Remove(frame, out (TClientInput input, long timestamp) rec))
            {
                previousInput_ = rec.input;
                time = rec.timestamp;
            }
            else
            {
                predictClientInput(ref previousInput_);
                time = null;
            }

            info.Input = previousInput_;
            return time;
        }
    }

    readonly Dictionary<int, SingleClientQueue> idToInputs_ = new();
    readonly List<int> removedClients_ = new();

    readonly double ticksPerSecond_;
    readonly PredictClientInputDelegate<TClientInput> predictClientInput_;

    readonly object mutex_ = new();

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="tps">The TPS the game should run at. Used for input delay calculations.</param>
    /// <param name="predictClientInput">Input predictor for client inputs. Used as a substitute when input of a client are not received in time.</param>
    /// <param name="onInputAuthored">Raised when given client input is being authored.</param>
    /// <param name="loggerFactory">Logger factory to use for logging events.</param>
    public ClientInputQueue(double tps, PredictClientInputDelegate<TClientInput> predictClientInput, InputAuthoredDelegate onInputAuthored, ILoggerFactory loggerFactory)
    {
        ticksPerSecond_ = tps;
        predictClientInput_ = predictClientInput;
        inputAuthored_ = onInputAuthored;
        logger_ = loggerFactory.CreateLogger<ClientInputQueue<TClientInput>>();
    }

    readonly InputAuthoredDelegate inputAuthored_;

    /// <summary>
    /// The latest constructed input frame number.
    /// </summary>
    public long Frame
    {
        get
        {
            lock (mutex_)
                return frame_;
        }
    }

    long frame_ = -1;

    readonly ILogger logger_;

    /// <summary>
    /// Add a client, which is from now expected to send input.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <exception cref="ArgumentException">If client with given id has already been added.</exception>
    public void AddClient(int id)
    {
        lock (mutex_)
        {
            if (!idToInputs_.TryAdd(id, new(predictClientInput_)))
            {
                logger_.LogCritical("To add duplicate client {Id}", id);
                throw new ArgumentException("Client with given id is already present.", nameof(id));
            }

            logger_.LogTrace("Added client {Id}.", id);
        }
    }

    /// <summary>
    /// Removes a client from the queue, discard all unretrieved inputs and disconnect the client.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <exception cref="ArgumentException">If the client id is not in the collection.</exception>
    public void RemoveClient(int id)
    {
        lock (mutex_)
        {

            if (!idToInputs_.Remove(id))
            {
                logger_.LogCritical("To remove non-contained {Id}", id);
                throw new ArgumentException("Client with given id is already present.", nameof(id));
            }

            removedClients_.Add(id);

            logger_.LogTrace("Removed client {Id}.", id);
        }
    }

    /// <summary>
    /// Adds an input for given client.
    /// </summary>
    /// <param name="id">ID of the client.</param>
    /// <param name="frame">Frame number to which the input corresponds.</param>
    /// <param name="input">The input.</param>
    public void AddInput(int id, long frame, TClientInput input)
    {
        lock (mutex_)
        {
            long now = Stopwatch.GetTimestamp();

            if (!idToInputs_.TryGetValue(id, out var clientInfo))
            {
                logger_.LogDebug("Got input from terminated client {Id} for {Frame} at {Current}..", id, frame, frame_);
                return;
            }

            if (frame <= frame_)
            {
                if (frame <= clientInfo.LastAuthorizedInput)
                    return; // No need to notify, notification has already been made.

                clientInfo.LastAuthorizedInput = frame;

                var framePart = Stopwatch.GetElapsedTime(lastFrameUpdate_, now);

                TimeSpan difference = TimeSpan.FromSeconds((frame - frame_) / ticksPerSecond_) - framePart;
                inputAuthored_?.Invoke(id, frame, difference);

                logger_.LogDebug( "Got late input from client {Id} for {Frame} at {Current} ({Time:F2} ms).", id, frame, frame_, difference.TotalMilliseconds);
                return;
            }
            
            if (!clientInfo.TryAdd(frame, input, now))
                return; // The input was already received.

            logger_.LogTrace("Got input from client {Id} for {Frame} at {Current}.", id, frame, frame_);
        }
    }

    long lastFrameUpdate_ = long.MaxValue;

    /// <summary>
    /// Constructs the next input frame out of collected inputs, any late inputs will be ignored.
    /// </summary>
    /// <returns>Input frame for current frame.</returns>
    public Memory<UpdateClientInfo<TClientInput>> ConstructAuthoritativeFrame()
    {
        lock (mutex_)
        {
            lastFrameUpdate_ = Stopwatch.GetTimestamp();
            
            int length = idToInputs_.Count + removedClients_.Count;

            Memory<UpdateClientInfo<TClientInput>> frame = new UpdateClientInfo<TClientInput>[length];

            long nextFrame = frame_ + 1;

            var span = frame.Span;

            int i = 0;
            foreach ((int id, SingleClientQueue queue) in idToInputs_)
            {
                long? timestamp = queue.WriteUpdateInfo(nextFrame, ref span[i]);
                span[i].Id = id;

                if (timestamp is { } value)
                {
                    queue.LastAuthorizedInput = nextFrame;
                    TimeSpan difference = Stopwatch.GetElapsedTime(value, lastFrameUpdate_);
                    inputAuthored_?.Invoke(id, nextFrame, difference);

                    logger_.LogTrace("Input from {Id} received {Time:F2} ms in advance.", id, difference.TotalMilliseconds);
                }
                
                i++;
            }

            foreach (int id in removedClients_)
            {
                span[i] = new(id, new(), true);
                i++;
            }
            
            frame_ = nextFrame;
            removedClients_.Clear();

            logger_.LogTrace("Constructed authoritative frame for {FrameIndex}.", nextFrame);

            return frame;
        }
    }
}
