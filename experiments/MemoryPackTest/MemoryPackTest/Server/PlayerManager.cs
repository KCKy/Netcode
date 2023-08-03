using MemoryPack;
using System;
using System.Collections.Generic;

namespace FrameworkTest;

public sealed class PlayerManager<TPlayerInput>
    where TPlayerInput : notnull, new()
{ 
    public int EarlyFrameOffset { get; init; } = 5;

    readonly Dictionary<long, Dictionary<long, TPlayerInput>> idToInputs_ = new();
    readonly List<long> removedPlayers_ = new();

    readonly object mutex_ = new();

    readonly IServerSession session_;

    public PlayerManager(IServerSession session)
    {
        session_ = session;
    }

    public long Frame { get; private set; } = -1;

    public void AddPlayer(long id)
    {
        lock (mutex_)
            if (!idToInputs_.TryAdd(id, new()))
                throw new ArgumentException("Player with given id is already present.", nameof(id));
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
            if (frame <= Frame)
            {
                Console.WriteLine($"Received late input from player {id} for frame {frame} at frame {Frame}.");
                session_.SignalMissedInput(id, frame, Frame);
                return;
            }

            /*if (frame > Frame + EarlyFrameOffset)
            {
                Console.WriteLine($"Received early input from player {id} for frame {frame} at frame {Frame}.");
                session_.SignalEarlyInput(id, frame, Frame);
            }*/
            
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
                return;
            }

            //Console.WriteLine($"Received input from player {id} for frame {frame}.");
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

            long nextFrame = Frame + 1;

            foreach ((long id, var value) in idToInputs_)
            {
                if (value.TryGetValue(nextFrame, out var input))
                {
                    //Console.WriteLine($"Player input of {id} for frame {nextFrame}: {input}.");
                    value.Remove(nextFrame);
                }
                else
                {
                    //Console.WriteLine($"Player missed input for frame {nextFrame}.");
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

            Frame = nextFrame;
            removedPlayers_.Clear();

            return inputFrame;
        }
    }
}
