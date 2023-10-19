using Core.Extensions;
using HashDepot;

namespace Core.Utility;

sealed class StateHolder<TC, TS, TG> : IStateHolder<TC, TS, TG>
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    public TG State
    {
        get => state_;
        set
        {
            state_ = value;
            checksum_ = null;
            serialized_ = null;
        }
    }

    public long Frame { get; set; } = -1;
    Memory<byte>? serialized_ = null;
    long? checksum_ = null;
    TG state_ = new();

    PooledBufferWriter<byte> writer_ = new();

    public Memory<byte> Serialize()
    {
        if (serialized_ is { } value)
            return value;
        
        var serialized = writer_.MemoryPackSerialize(state_);
        SetHash(serialized.Span);
        
        return serialized;
    }

    void SetHash(Span<byte> serialized)
    {
        checksum_ = (long)XXHash.Hash64(serialized);
    }

    public long GetChecksum()
    {
        if (checksum_ is not null)
            return checksum_.Value;

        serialized_ = writer_.MemoryPackSerialize(state_);
        SetHash(serialized_.Value.Span);
        
        return checksum_!.Value;
    }

    public UpdateOutput Update(UpdateInput<TC, TS> input)
    {
        Frame++;
        checksum_ = null;
        serialized_ = null;
        return State.Update(input);
    }
}
