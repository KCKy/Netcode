using System;
using HashDepot;
using Kcky.Useful;
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Utility;

interface IStateHolderType { }

struct ServerStateType : IStateHolderType { }
struct AuthoritativeStateType : IStateHolderType { }
struct PredictiveStateType : IStateHolderType  { }
struct ReplacementStateType : IStateHolderType  { }
struct MiscStateType : IStateHolderType  { }

/// <summary>
/// Owner of a specific game state, keeps its index, provides methods for updating, checksums and serialization.
/// </summary>
sealed class StateHolder<TClientInput, TServerInput, TGameState, TTypeTag>(ILoggerFactory loggerFactory)
    where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new()
    where TTypeTag: struct, IStateHolderType
{
    public TGameState State
    {
        get => state_;
        set
        {
            state_ = value;
            checksum_ = null;
        }
    }

    public long Frame { get; set; } = -1;

    TGameState state_ = new();

    readonly PooledBufferWriter<byte> writer_ = new();
    bool serialized_ = false;
    long? checksum_ = null;
    
    readonly ILogger logger_ = loggerFactory.CreateLogger<StateHolder<TClientInput, TServerInput, TGameState, TTypeTag>>();
    
    public Memory<byte> GetSerialized()
    {
        if (!serialized_)
            MemoryPackSerializer.Serialize(writer_, state_);

        if (checksum_ is null)
            CalculateChecksum();

        serialized_ = false;
        return writer_.ExtractAndReplace();
    }

    void CalculateChecksum()
    {
        checksum_ = (long)XXHash.Hash64(writer_.WrittenSpan);
    }

    public long GetChecksum()
    {
        if (checksum_ is { } calculatedChecksum)
            return calculatedChecksum;

        if (!serialized_)
        {
            MemoryPackSerializer.Serialize(writer_, state_);
            serialized_ = true;
        }

        CalculateChecksum();
        
        return checksum_!.Value;
    }

    public UpdateOutput Update(UpdateInput<TClientInput, TServerInput> input)
    {
        Frame++;
        checksum_ = null;
        serialized_ = false;
        writer_.Reset();
        
        try
        {
            return State.Update(input, logger_);
        }
        catch (Exception ex)
        {
            logger_.LogError(ex, "State update failed with an exception!");
        }
        
        return UpdateOutput.Empty;
    }
}
