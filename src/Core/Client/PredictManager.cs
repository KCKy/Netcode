using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using Core.DataStructures;
using Core.Providers;
using Core.Transport;
using Core.Utility;
using MemoryPack;
using Serilog;
using Useful;

namespace Core.Client;

sealed class PredictManager<TC, TS, TG> : IPredictManager<TC, TS, TG>
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly ILogger Logger = Log.ForContext<PredictManager<TC, TS, TG>>();

    /*
     * Mutex ordering to avoid deadlocks:
     * Tick Mutex
     * Replacement State
     * Predict State
     * Replacement Mutex
     */

    /// This mutex assures predict ticks are not executed concurrently,
    /// Which may happen in some extreme edge cases.
    readonly object tickMutex_ = new();

    /// Predict queue is thread safe, but only one task (predict or replace) is allowed to manage its predictions, until it is superseded.
    /// This mutex assures atomicity of replace identification and predict queue write access.
    readonly object replacementMutex_ = new();

    readonly ConcurrentQueue<Memory<byte>> predictQueue_ = new();
    
    /// The mutex of replacement state shall be held during the whole duration of a replacement task, and should be released only after the task is finished.
    /// It provides access to the content of the holder.
    /// <see cref="replacementMutex_"/> should be acquired every now and then to check whether the replacement is still required.
    readonly IStateHolder<TC, TS, TG> replacementState_ = new StateHolder<TC, TS, TG>();
    long currentReplacement_ = 0;
    bool activeReplacement_ = false;
    ///

    // Mutex of this object shall be held when the holder or input is checked or updated to assure atomicity and validity.
    readonly IStateHolder<TC, TS, TG> predictState_ = new StateHolder<TC, TS, TG>();
    UpdateInput<TC, TS> predictInput_ = UpdateInput<TC, TS>.Empty;
    //
    
    // Mutex of auth state holder must be acquired before we can read it.
    public required IStateHolder<TC, TS, TG> AuthState { private get; init; }
    //
    
    // Following objects are thread safe
    public required IClientInputPredictor<TC> InputPredictor { private get; init; }
    public required IServerInputPredictor<TS, TG> ServerInputPredictor { private get; init; }
    public required IClientSender Sender { private get; init; }
    //

    /// <inheritdoc/>
    public long LocalId { private get; set; }
    
    // This object is exclusive to predict.
    public required IClientInputProvider<TC> InputProvider { private get; init; }

    // Making new client inputs is exclusive to predict update.
    public required ILocalInputQueue<TC> ClientInputs { private get; init; }

    // Displaying is exclusive to the predict update.
    public required IDisplayer<TG> Displayer { private get; init; }
    
    /// <inheritdoc/>
    public void Init(long frame, TG state)
    {
        lock (predictState_)
        {
            predictState_.Frame = frame;
            predictState_.State = state;
        }

        ClientInputs.Set(frame);

        predictQueue_.Clear();

        Logger.Debug("Initiated predict state.");
    }

    public long Frame
    {
        get
        {
            lock (predictState_)
                return predictState_.Frame;
        }
    }

    /// <inheritdoc/>
    public void InformAuthInput(ReadOnlySpan<byte> serializedInput, long frame, UpdateInput<TC, TS> input)
    {
        if (!predictQueue_.TryDequeue(out var predictedInput))
            predictedInput = Memory<byte>.Empty;

        if (predictedInput.Span.SequenceEqual(serializedInput))
            return;

        Logger.Debug("Divergence appeared for frame {Frame}.", frame);

        // We have a divergence, a new replacement is required.

        BeginReplacement(frame, input);
    }

    void BeginReplacement(long frame, UpdateInput<TC, TS> input)
    {
        // Acquire index now, to assure correct ordering of replacements. 
        long index;
        lock (replacementMutex_)
        {
            index = ++currentReplacement_;
            activeReplacement_ = true; // Disable predict input generation
            predictQueue_.Clear();
        }

        // This is only called synchronously within the state update lock, so no further locking should be needed
        Debug.Assert(AuthState.Frame == frame);
        var authState = AuthState.Serialize();

        ReplaceGameStateAsync(index, frame, authState, input).AssureSuccess();
    }

    readonly PooledBufferWriter<byte> replacementInputWriter_ = new();
    readonly PooledBufferWriter<byte> predictInputWriter_ = new();

    async Task ReplaceGameStateAsync(long replacementIndex, long frame, Memory<byte> serializedState, UpdateInput<TC, TS> input)
    {
        await Task.Yield(); // RunAsync in a different thread

        Logger.Debug("Began replacement for frame {Frame}.", frame);

        // Wait for earlier replacements to finish
        lock (replacementState_)
        {
            // Check ownership
            lock (replacementMutex_)
            {
                if (currentReplacement_ > replacementIndex)
                    return;
            }

            ClientInputs.Pop(frame); // Only inputs greater than frame will be needed from now.

            TG? state = replacementState_.State;

            MemoryPackSerializer.Deserialize(serializedState.Span, ref state);
            ArrayPool<byte>.Shared.Return(serializedState);

            // Prepare replacement state
            if (state is null)
                throw new ArgumentException("Failed to copy state.", nameof(serializedState));
            
            replacementState_.Frame = frame;
            replacementState_.State = state;

            // Begin replacement loop
            while (true)
            {
                // Check how many steps remain
                long difference;
                lock (predictState_)
                {
                    difference = predictState_.Frame - frame;

                    if (difference == 0)
                    {
                        Debug.Assert(predictState_.Frame == replacementState_.Frame);

                        // Replacement was successful
                        (replacementState_.State, predictState_.State) = (predictState_.State, replacementState_.State);
                        predictInput_ = input;
                        
                        lock (replacementMutex_)
                            if (currentReplacement_ <= replacementIndex)
                                activeReplacement_ = false; // Replacement has not been superseded yet. Return predict queue ownership to predict queue.
                        
                        Debug.Assert(frame == replacementState_.Frame);

                        Logger.Debug("Successfully replaced predict at frame {Frame}.", frame);
                        return;
                    }
                }

                if (difference < 0)
                {
                    Logger.Error("Predict state is behind replacement state. This should not happen. {Replacement} {Predict}", replacementState_.Frame, predictState_.Frame);
                    return;
                }
            
                // Replacement state is behind predict we need to update

                Logger.Debug("Need to catchup {Updates} updates.", difference);

                while (difference > 0)
                {
                    frame++;
                    difference--;

                    // This is assured to exist
                    TC localInput = ClientInputs[frame] ?? new();

                    // Predict
                    PredictClientInput(input.ClientInput.Span, localInput);
                    ServerInputPredictor.PredictInput(ref input.ServerInput, replacementState_.State);

                    MemoryPackSerializer.Serialize(replacementInputWriter_, input);

                    lock (replacementMutex_)
                    {
                        if (currentReplacement_ > replacementIndex)
                        {
                            replacementInputWriter_.Reset();
                            return;
                        }

                        // If were still current, we may add our prediction

                        predictQueue_.Enqueue(replacementInputWriter_.ExtractAndReplace());
                    }

                    replacementState_.Update(input);

                    Debug.Assert(frame == replacementState_.Frame);
                }
            }
        }
    }

    void PredictUpdate()
    {
        // Input
        TC localInput = InputProvider.GetInput();
        long frame = predictState_.Frame + 1;
        ClientInputs.Add(localInput, frame);

        // Send
        Sender.SendInput(frame, localInput);

        // Modify
        Logger.Verbose("Updating predict at frame {frame}.", frame);
            
        lock (predictState_)
        {
            // Predict other input
            PredictClientInput(predictInput_.ClientInput.Span, localInput);
            ServerInputPredictor.PredictInput(ref predictInput_.ServerInput, predictState_.State);
            
            // Update
            predictState_.Update(predictInput_);

            // Display
            Displayer.AddPredict(frame, predictState_.State);

            // Save prediction input if this timeline is not stale
            MemoryPackSerializer.Serialize(predictInputWriter_, predictInput_);

            lock (replacementMutex_)
            {
                if (!activeReplacement_)
                {
                    var serialized = predictInputWriter_.ExtractAndReplace();
                    predictQueue_.Enqueue(serialized);
                }
                else
                {
                    predictInputWriter_.Reset();
                }
            }
        }
    }

    public void Tick()
    {
        lock (tickMutex_)
            PredictUpdate();
    }

    public void Stop()
    {
        lock (replacementMutex_)
            currentReplacement_ = long.MaxValue;
    }
    
    void PredictClientInput(Span<UpdateClientInfo<TC>> clientInputs, TC localInput)
    {
        // Terminated players are going to be predicted as well, but the game state update should ignore the removed player.

        long localId = LocalId;

        int length = clientInputs.Length;
        for (int i = 0; i < length; i++)
        {
            ref var info = ref clientInputs[i];

            if (info.Id == localId)
            {
                info.Input = localInput;
            }
            else
            {
                InputPredictor.PredictInput(ref info.Input);
            }
        }
    }
}
