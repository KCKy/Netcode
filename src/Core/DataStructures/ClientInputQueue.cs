using System.Buffers;
using System.Diagnostics;
using Core.Extensions;
using Core.Utility;

namespace Core.DataStructures;

/// <summary>
/// Receives all client input to the server. Constructs authoritative client update inputs <see cref="UpdateClientInfo{TPlayerInput}"/>.
/// </summary>
/// <typeparam name="TPlayerInput"></typeparam>
public sealed class ClientInputQueue<TPlayerInput>
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

    readonly object mutex_ = new();

    /// <summary>
    /// The latest contructed input frame number.
    /// </summary>
    public long Frame { get; private set; } = -1;

    /// <summary>
    /// Add a client, which is from now expected to send input.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <exception cref="ArgumentException">If client with given id has already been added.</exception>
    public void AddPlayer(long id)
    {
        lock (mutex_)
            if (!idToInputs_.TryAdd(id, new()))
                throw new ArgumentException("Player with given id is already present.", nameof(id));
    }

    /// <summary>
    /// Removes a client from the queue, discard all unretrieved inputs and disconnect the player.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <exception cref="ArgumentException">If the client id is not in the collection.</exception>
    public void RemovePlayer(long id)
    {
        lock (mutex_)
        {
            if (!idToInputs_.Remove(id))
                throw new ArgumentException("Given id is not in the collection.", nameof(id));

            removedPlayers_.Add(id);
        }
    }

    /// <summary>
    /// Adds an input for given client.
    /// </summary>
    /// <param name="id">Id of the client.</param>
    /// <param name="frame">Frame number to which the input corresponds.</param>
    /// <param name="input">The input.</param>
    public void AddInput(long id, long frame, TPlayerInput input)
    {
        lock (mutex_)
        {
            if (frame <= Frame)
            {
                Debug.WriteLine($"Received late input from player {id} for frame {frame} at frame {Frame}.");
                return;
            }

            if (!idToInputs_.TryGetValue(id, out var frameToInput))
            {
                Debug.WriteLine($"Received input from terminated player {id} for frame {frame}.");
                return;
            }

            if (!frameToInput.TryAdd(frame, input))
            {
                Debug.WriteLine($"Received repeated input from player {id} for frame {frame}.");
                return;
            }

            // TODO: add extra tracing
        }
    }

    /// <summary>
    /// Constructs the next input frame out of collected inputs, any late inputs will be ignored.
    /// </summary>
    /// <returns>Input frame for current frame.</returns>
    public Memory<UpdateClientInfo<TPlayerInput>> ConstructAuthoritativeFrame()
    {
        lock (mutex_)
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

            return frame;
        }
    }
}
