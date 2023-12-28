using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;
using Core.Providers;
using Core.Utility;
using Serilog;

namespace Core.DataStructures;

/// <summary>
/// Receives all client input to the server. Constructs authoritative client update inputs <see cref="UpdateClientInfo{TClientInput}"/>.
/// </summary>
/// <typeparam name="TClientInput"></typeparam>
public interface IClientInputQueue<TClientInput>
where TClientInput : class, new()
{
    /// <summary>
    /// The latest constructed input frame number.
    /// </summary>
    long Frame { get; }

    /// <summary>
    /// Add a client, which is from now expected to send input.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <exception cref="ArgumentException">If client with given id has already been added.</exception>
    void AddClient(long id);

    /// <summary>
    /// Removes a client from the queue, discard all unretrieved inputs and disconnect the client.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <exception cref="ArgumentException">If the client id is not in the collection.</exception>
    void RemoveClient(long id);

    /// <summary>
    /// Adds an input for given client.
    /// </summary>
    /// <param name="id">Id of the client.</param>
    /// <param name="frame">frame number to which the input corresponds.</param>
    /// <param name="input">The input.</param>
    void AddInput(long id, long frame, TClientInput input);

    /// <summary>
    /// Constructs the next input frame out of collected inputs, any late inputs will be ignored.
    /// </summary>
    /// <returns>Input frame for current frame.</returns>
    Memory<UpdateClientInfo<TClientInput>> ConstructAuthoritativeFrame();
    
    event InputAuthoredDelegate OnInputAuthored;
}

public delegate void InputAuthoredDelegate(long id, long frame, TimeSpan difference);

/// <inheritdoc/>
public sealed class ClientInputQueue<TClientInput> : IClientInputQueue<TClientInput>
where TClientInput : class, new()
{
    sealed class SingleClientQueue
    {
        public long LastAuthorizedInput = long.MinValue;
        readonly IClientInputPredictor<TClientInput> predictor_;
        readonly Dictionary<long, (TClientInput input, long timestamp)> frameToInput_ = new();
        TClientInput previousInput_ = new();

        public SingleClientQueue(IClientInputPredictor<TClientInput> predictor)
        {
            predictor_ = predictor;
        }

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
                predictor_.PredictInput(ref previousInput_);
                time = null;
            }

            info.Input = previousInput_;
            return time;
        }
    }

    readonly Dictionary<long, SingleClientQueue> idToInputs_ = new();
    readonly List<long> removedClients_ = new();

    readonly double ticksPerSecond_;
    readonly IClientInputPredictor<TClientInput> predictor_;

    readonly object mutex_ = new();

    public ClientInputQueue(double tps, IClientInputPredictor<TClientInput> predictor)
    {
        ticksPerSecond_ = tps;
        predictor_ = predictor;
    }

    /// <inheritdoc/>/// 
    public long Frame
    {
        get
        {
            lock (mutex_)
                return frame_;
        }
    }

    long frame_ = -1;

    readonly ILogger logger_ = Log.ForContext<ClientInputQueue<TClientInput>>();

    /// <inheritdoc/>
    public void AddClient(long id)
    {
        lock (mutex_)
        {
            if (!idToInputs_.TryAdd(id, new(predictor_)))
            {
                logger_.Fatal("To add duplicate client {Id}", id);
                throw new ArgumentException("Client with given id is already present.", nameof(id));
            }

            logger_.Verbose("Added client {Id}.", id);
        }
    }

    /// <inheritdoc/>
    public void RemoveClient(long id)
    {
        lock (mutex_)
        {

            if (!idToInputs_.Remove(id))
            {
                logger_.Fatal("To remove non-contained {Id}", id);
                throw new ArgumentException("Client with given id is already present.", nameof(id));
            }

            removedClients_.Add(id);

            logger_.Verbose("Removed client {Id}.", id);
        }
    }

    /// <inheritdoc/>
    public void AddInput(long id, long frame, TClientInput input)
    {
        lock (mutex_)
        {
            long timestamp = Stopwatch.GetTimestamp();

            if (!idToInputs_.TryGetValue(id, out var clientInfo))
            {
                logger_.Debug("Got input from terminated client {Id} for {Frame} at {Current}..", id, frame, frame_);
                return;
            }

            if (frame <= frame_)
            {
                long now = Stopwatch.GetTimestamp();

                if (frame <= clientInfo.LastAuthorizedInput)
                    return; // No need to notify, notification has already been made.

                clientInfo.LastAuthorizedInput = frame;

                var framePart = Stopwatch.GetElapsedTime(lastFrameUpdate_, timestamp);

                TimeSpan difference = TimeSpan.FromSeconds((frame - frame_) / ticksPerSecond_) - framePart;
                OnInputAuthored?.Invoke(id, frame, difference);

                logger_.Debug( "Got late input from client {Id} for {Frame} at {Current} ({Time:F2} ms).", id, frame, frame_, difference.TotalMilliseconds);
                return;
            }
            
            if (!clientInfo.TryAdd(frame, input, timestamp))
            {
                //logger_.Verbose("Got repeated input from client {Id} for {Frame} at {Current}..", id, frame, frame_);
                return;
            }

            logger_.Verbose("Got input from client {Id} for {Frame} at {Current}.", id, frame, frame_);
        }
    }

    long lastFrameUpdate_ = long.MaxValue;

    /// <inheritdoc/>
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
            foreach ((long id, SingleClientQueue queue) in idToInputs_)
            {
                long? timestamp = queue.WriteUpdateInfo(nextFrame, ref span[i]);
                span[i].Id = id;

                if (timestamp is {} value)
                {
                    queue.LastAuthorizedInput = nextFrame;
                    TimeSpan difference = Stopwatch.GetElapsedTime(value, lastFrameUpdate_);
                    OnInputAuthored?.Invoke(id, nextFrame, difference);

                    logger_.Verbose("Input from {Id} received {Time:F2} ms in advance.", id, difference.TotalMilliseconds);
                }
                
                i++;
            }

            foreach (long id in removedClients_)
                span[i] = new(id, new(), true);

            frame_ = nextFrame;
            removedClients_.Clear();

            logger_.Verbose("Constructed authoritative frame for {FrameIndex}.", nextFrame);

            return frame;
        }
    }

    public event InputAuthoredDelegate? OnInputAuthored;
}
