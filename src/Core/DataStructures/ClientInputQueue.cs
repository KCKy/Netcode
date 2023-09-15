using System.Buffers;
using System.Diagnostics;
using Core.Extensions;
using Core.Utility;
using Serilog;

namespace Core.DataStructures;

/// <summary>
/// Receives all client input to the server. Constructs authoritative client update inputs <see cref="UpdateClientInfo{TPlayerInput}"/>.
/// </summary>
/// <typeparam name="TPlayerInput"></typeparam>
public interface IClientInputQueue<TPlayerInput>
where TPlayerInput : class, new()
{
    /// <summary>
    /// The latest contructed input frame number.
    /// </summary>
    long Frame { get; }

    /// <summary>
    /// Add a client, which is from now expected to send input.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <exception cref="ArgumentException">If client with given id has already been added.</exception>
    void AddClient(long id);

    /// <summary>
    /// Removes a client from the queue, discard all unretrieved inputs and disconnect the player.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <exception cref="ArgumentException">If the client id is not in the collection.</exception>
    void RemoveClient(long id);

    /// <summary>
    /// Adds an input for given client.
    /// </summary>
    /// <param name="id">Id of the client.</param>
    /// <param name="frame">Frame number to which the input corresponds.</param>
    /// <param name="input">The input.</param>
    void AddInput(long id, long frame, TPlayerInput input);

    /// <summary>
    /// Constructs the next input frame out of collected inputs, any late inputs will be ignored.
    /// </summary>
    /// <returns>Input frame for current frame.</returns>
    Memory<UpdateClientInfo<TPlayerInput>> ConstructAuthoritativeFrame();
}

/// <inheritdoc/>
public sealed class ClientInputQueue<TPlayerInput> : IClientInputQueue<TPlayerInput>
where TPlayerInput : class, new()
{
    readonly struct SingleClientQueue
    {
        readonly Dictionary<long, TPlayerInput> frameToInput_ = new();

        public SingleClientQueue() { }

        public bool TryAdd(long frame, TPlayerInput input) => frameToInput_.TryAdd(frame, input);

        public void WriteUpdateInfo(long frame, ref UpdateClientInfo<TPlayerInput> info)
        {
            if (!frameToInput_.Remove(frame, out info.Input!))
                info.Input = DefaultProvider<TPlayerInput>.Create();
        }
    }

    readonly Dictionary<long, SingleClientQueue> idToInputs_ = new();
    readonly List<long> removedPlayers_ = new();

    /// <inheritdoc/>
    public long Frame { get; private set; } = -1;

    readonly ILogger logger_ = Log.ForContext<ClientInputQueue<TPlayerInput>>();

    /// <inheritdoc/>
    public void AddClient(long id)
    {
        if (!idToInputs_.TryAdd(id, new()))
        {
            logger_.Fatal("To add duplicate client {Id}", id);
            throw new ArgumentException("Player with given id is already present.", nameof(id));
        }

         logger_.Verbose("Added client {Id}.", id);
    }

    /// <inheritdoc/>
    public void RemoveClient(long id)
    {
        if (!idToInputs_.Remove(id))
        {
            logger_.Fatal("To remove non-contained {Id}", id);
            throw new ArgumentException("Player with given id is already present.", nameof(id));
        }

        removedPlayers_.Add(id);

        logger_.Verbose("Removed client {Id}.", id);
    }

    /// <inheritdoc/>
    public void AddInput(long id, long frame, TPlayerInput input)
    {
        if (frame <= Frame)
        {
            logger_.Debug("Got late {Input} from player {Id} for {Frame} at {Frame} at {Current}.", input, id, frame, Frame);
            return;
        }

        if (!idToInputs_.TryGetValue(id, out var frameToInput))
        {
            logger_.Debug("Got {Input} from terminated player {Id} for {Frame} at {Current}..", input, id, frame, Frame);
            return;
        }

        if (!frameToInput.TryAdd(frame, input))
        {
            logger_.Debug("Got repeated {Input} from player {Id} for {Frame} at {Current}..", input, id, frame, Frame);
            return;
        }

        logger_.Verbose("Got {Input} from player {Id} for {Frame} at {Current}.", input, id, frame, Frame);
    }

    /// <inheritdoc/>
    public Memory<UpdateClientInfo<TPlayerInput>> ConstructAuthoritativeFrame()
    {
        int length = idToInputs_.Count + removedPlayers_.Count;

        var frame = ArrayPool<UpdateClientInfo<TPlayerInput>>.Shared.RentMemory(length);

        long nextFrame = Frame + 1;

        var span = frame.Span;

        int i = 0;
        foreach ((long id, SingleClientQueue queue) in idToInputs_)
        {
            queue.WriteUpdateInfo(nextFrame, ref span[i]);
            span[i].Id = id;
            i++;
        }

        foreach (long id in removedPlayers_)
            span[i] = new(id, DefaultProvider<TPlayerInput>.Create(), true);

        Frame = nextFrame;
        removedPlayers_.Clear();

        logger_.Verbose("Constructed authoritative {Frame} for {FrameIndex}.", frame, nextFrame);

        return frame;
    }
}
