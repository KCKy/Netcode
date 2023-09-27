using HashDepot;
using MemoryPack;

namespace Core.Utility;

sealed class StateHolder<TC, TS, TG> : IStateHolder<TC, TS, TG>
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    public TG State { get; set; } = new();

    public long Frame { get; set; } = -1;

    long? checksum_ = null;

    public Memory<byte> Serialize()
    {
        Memory<byte> serialized = MemoryPackSerializer.Serialize(State);
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

        Span<byte> serialized = MemoryPackSerializer.Serialize(State);
        SetHash(serialized);

        return checksum_!.Value;
    }

    public UpdateOutput Update(UpdateInput<TC, TS> input)
    {
        Frame++;
        checksum_ = null;
        return State.Update(input);
    }
}
