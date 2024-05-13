using System;
using HashDepot;
using Kcky.Useful;
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Utility;

/// <summary>
/// Owner of a specific game state, keeps its index, provides methods for updating, checksums and serialization.
/// </summary>
sealed class StateHolder<TC, TS, TG>(ILoggerFactory loggerFactory)
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
        }
    }

    public long Frame { get; set; } = -1;

    TG state_ = new();

    readonly PooledBufferWriter<byte> writer_ = new();
    bool serialized_ = false;
    long? checksum_ = null;
    
    readonly ILogger logger_ = loggerFactory.CreateLogger<StateHolder<TC, TS, TG>>();
    
    public Memory<byte> GetSerialized()
    {
        if (!serialized_)
            MemoryPackSerializer.Serialize(writer_, state_);

        if (checksum_ is null)
            CalculateChecksum();

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

    public UpdateOutput Update(UpdateInput<TC, TS> input)
    {
        Frame++;
        checksum_ = null;
        serialized_ = false;
        writer_.Reset();

        try
        {
            return State.Update(input);
        }
        catch (Exception ex)
        {
            logger_.LogError(ex, "State update failed with an exception!");
        }

        return UpdateOutput.Empty;
    }
}
