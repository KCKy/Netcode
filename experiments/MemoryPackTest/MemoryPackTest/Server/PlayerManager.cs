using MemoryPack;
using System;
using System.Collections.Generic;

namespace FrameworkTest;

class PlayerManager<TPlayerInput>
    where TPlayerInput : notnull, new()
{
    const int BasePlayerCapacity = 100;
    const int BaseFrameCapacity = 1000;
    public int EarlyFrameOffset { get; init; } = 5;

    readonly Dictionary<long, Dictionary<long, TPlayerInput>> idToInputs_ = new(BasePlayerCapacity);
    readonly List<long> removedPlayers_ = new(BasePlayerCapacity);

    readonly object mutex_ = new();

    readonly IServerSession session_;

    public PlayerManager(IServerSession session)
    {
        session_ = session;
    }

    public long Frame { get; private set; } = 0;

    public void AddPlayer(long id)
    {
        lock (mutex_)
            if (!idToInputs_.TryAdd(id, new(BaseFrameCapacity)))
                throw new ArgumentException("Player with given id is already present.", nameof(id));

        // TODO: send initial state
    }

    public void TerminatePlayer(long id)
    {
        lock (mutex_)
        {
            if (!idToInputs_.Remove(id))
            {
                removedPlayers_.Add(id);
            }
            else
            {
                throw new ArgumentException("Player with given id was not present.", nameof(id));
            }
        }
    }

    public void AddPlayerInput(long id, long frame, ReadOnlyMemory<byte> inputData)
    {
        lock (mutex_)
        {
            if (frame < Frame)
            {
                Console.WriteLine($"Received late input from player {id} for frame {frame} at frame {Frame}.");
                session_.SignalMissedInput(id, frame, Frame);
                return;
            }

            if (frame > Frame + EarlyFrameOffset)
            {
                Console.WriteLine($"Received early input from player {id} for frame {frame} at frame {Frame}.");
                session_.SignalMissedInput(id, frame, Frame);
                return;
            }
            
            if (!idToInputs_.TryGetValue(id, out var frameToInput))
            {
                Console.WriteLine($"Received input from terminated player {id} for frame {frame}.");
                return;
            }

            if (MemoryPackSerializer.Deserialize<TPlayerInput>(inputData.Span) is not TPlayerInput playerInput)
            {
                Console.WriteLine($"Received invalid input from player {id} for frame {frame}: {Convert.ToHexString(inputData.Span)}");
                return;
            }

            if (!frameToInput.TryAdd(frame, playerInput))
            {
                Console.WriteLine($"Received repeated input from player {id} for frame {frame}.");
            }
        }
    }

    public void SendGameInput(ReadOnlyMemory<byte> input, long frame)
    {
        lock (mutex_)
            foreach (long id in idToInputs_.Keys)
                session_.SendInputToPlayer(id, frame, input);
    }

    public (long Id, TPlayerInput Input, bool Terminated)[] ConstructAuthoritativeInputFrame()
    {
        lock (mutex_)
        {
            int length = idToInputs_.Count + removedPlayers_.Count;
            var inputFrame = new (long, TPlayerInput, bool)[length];

            int i = 0;

            foreach ((long id, var value) in idToInputs_)
            {
                if (value.TryGetValue(Frame, out var input))
                {
                    value.Remove(Frame);
                }
                else
                {
                    input = new();
                }
                
                inputFrame[i] = (id, input, false);
                i++;
            }

            foreach (long id in removedPlayers_)
            {
                inputFrame[i] = (id, new(), true);
                i++;
            }

            Frame++;
            removedPlayers_.Clear();

            return inputFrame;
        }
    }
}
