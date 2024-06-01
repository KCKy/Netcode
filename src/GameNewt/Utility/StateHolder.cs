using System;
using HashDepot;
using Kcky.Useful;
using MemoryPack;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Utility;

/// <summary>
/// Tag for <see cref="StateHolder{TClientInput, TServerInput, TGameState, TTypeTag}"/> to describes it use-case.
/// </summary>
interface IStateHolderType { }

/// <summary>
/// Tag for server side authoritative state.
/// </summary>
struct ServerStateType : IStateHolderType { }

/// <summary>
/// Tag for client side authoritative state.
/// </summary>
struct AuthoritativeStateType : IStateHolderType { }

/// <summary>
/// Tag for client side predictive state.
/// </summary>
struct PredictiveStateType : IStateHolderType  { }

/// <summary>
/// Tag for client side state mean to replace the predictive.
/// </summary>
struct ReplacementStateType : IStateHolderType  { }

/// <summary>
/// Tag for a state which does not fit other tag descriptions.
/// </summary>
struct MiscStateType : IStateHolderType  { }

/// <summary>
/// Owner of a specific game state, keeps its index, provides methods for updating, checksums and serialization.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
/// <typeparam name="TTypeTag">Tag to describe this state holder use case.</typeparam>
/// <param name="loggerFactory">Logger factory to use for logging purposes.</param>
sealed class StateHolder<TClientInput, TServerInput, TGameState, TTypeTag>(ILoggerFactory loggerFactory)
    where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new()
    where TTypeTag: struct, IStateHolderType
{
    /// <summary>
    /// The held state.
    /// </summary>
    public TGameState State
    {
        get => state_;
        set
        {
            state_ = value;
            checksum_ = null;
        }
    }

    /// <summary>
    /// The frame number the state corresponds to.
    /// </summary>
    public long Frame { get; set; } = -1;

    TGameState state_ = new();
    readonly PooledBufferWriter<byte> writer_ = new();
    long? checksum_ = null;
    readonly ILogger logger_ = loggerFactory.CreateLogger<StateHolder<TClientInput, TServerInput, TGameState, TTypeTag>>();
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public Memory<byte> GetSerialized()
    {
        if (writer_.WrittenSpan.IsEmpty)
            MemoryPackSerializer.Serialize(writer_, state_);

        checksum_ ??= (long)XXHash.Hash64(writer_.WrittenSpan);

        return writer_.ExtractAndReplace();
    }

    /// <summary>
    /// Provides checksum for the current state.
    /// </summary>
    /// <returns>Deterministic checksum of this state.</returns>
    public long GetChecksum()
    {
        if (checksum_ is { } calculatedChecksum)
            return calculatedChecksum;

        if (writer_.WrittenSpan.IsEmpty)
            MemoryPackSerializer.Serialize(writer_, state_);

        checksum_ = (long)XXHash.Hash64(writer_.WrittenSpan);
        
        return checksum_!.Value;
    }

    /// <summary>
    /// Do a game state update.
    /// </summary>
    /// <param name="input">The inputs for the game state update.</param>
    /// <returns>The update output of this update.</returns>
    public UpdateOutput Update(UpdateInput<TClientInput, TServerInput> input)
    {
        Frame++;
        checksum_ = null;
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
