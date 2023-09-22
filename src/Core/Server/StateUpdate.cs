using HashDepot;
using MemoryPack;

namespace Core.Server;

sealed class StateUpdate<TPlayerInput, TServerInput, TGameState, TUpdateInfo> : IStateManager<TPlayerInput, TServerInput, TGameState, TUpdateInfo>
    where TGameState : class, IGameState<TPlayerInput, TServerInput, TUpdateInfo>, new()
    where TPlayerInput : class, new()
    where TServerInput : class, new()
    where TUpdateInfo : new()
{
    public TGameState State { get; } = new();

    public long Frame { get; private set; } = -1;

    long? checksum_ = null;

    public Memory<byte> Serialize()
    {
        Memory<byte> serialized = MemoryPackSerializer.Serialize(State);
        SetHash(serialized.Span);
        return serialized;
    }

    void SetHash(Span<byte> serialized)
    {
        checksum_ = (long) XXHash.Hash64(serialized);
    }

    public long GetChecksum()
    {
        if (checksum_ is not null)
            return checksum_.Value;

        Span<byte> serialized = MemoryPackSerializer.Serialize(State);
        SetHash(serialized);

        return checksum_!.Value;
    }
    
    public UpdateOutput Update(UpdateInput<TPlayerInput, TServerInput> input, ref TUpdateInfo info)
    {
        Frame++;
        checksum_ = null;
        return State.Update(input, ref info);
    }
}
