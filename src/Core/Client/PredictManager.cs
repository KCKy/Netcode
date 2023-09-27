using System.Diagnostics;
using Core.DataStructures;
using Core.Extensions;
using Core.Providers;
using Core.Transport;
using Core.Utility;
using MemoryPack;
using Serilog;

namespace Core.Client;

sealed class PredictManager<TC, TS, TG> : IPredictManager<TC, TS, TG>
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly ILogger logger_ = Log.ForContext<PredictManager<TC, TS, TG>>();

    /*
     * Mutex ordering to avoid deadlocks:
     * Tick Mutex
     * Auth State
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
    readonly IPredictQueue<Memory<byte>> predictQueue_ = null!;
    
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
    public required IClientDispatcher Dispatcher { private get; init; }
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

        ClientInputs.Pop(frame);

        predictQueue_.Reset(frame + 1);
    }

    /// <inheritdoc/>
    public void InformAuthInput(ReadOnlyMemory<byte> serializedInput, long frame, UpdateInput<TC, TS> input)
    {
        var predictedInput = predictQueue_.Dequeue(frame) ?? Memory<byte>.Empty;

        if (predictedInput.Span.SequenceEqual(serializedInput.Span))
            return;

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
            predictQueue_.Reset(frame + 1);
        }

        // This is only called synchronously within the state update lock, so no further locking should be needed
        Debug.Assert(AuthState.Frame == frame);
        Memory<byte> authState = AuthState.Serialize();

        ReplaceGameStateAsync(index, frame, authState, input).AssureSuccess();
    }

    async Task ReplaceGameStateAsync(long replacementIndex, long frame, Memory<byte> serializedState, UpdateInput<TC, TS> input)
    {
        await Task.Yield(); // RunAsync in a different thread

        // Wait for earlier replacements to finish
        lock (replacementState_)
        {
            ClientInputs.Pop(frame); // Only inputs greater than frame will be needed from now.

            // Check ownership
            lock (replacementMutex_)
                if (currentReplacement_ > replacementIndex)
                    return;

            // Prepare replacement state
            if (replacementState_.State.Replace(serializedState.Span) is not {} state)
            {
                const string failed = "Failed to copy state.";
                logger_.Fatal(failed);
                throw new ArgumentException(failed, nameof(serializedState));
            }
            replacementState_.Frame = frame;
            replacementState_.State = state;

            // Begin replacement loop
            while (true)
            {
                // Check how many steps remain
                long difference;
                lock (predictState_)
                {
                    difference = predictState_.Frame - replacementState_.Frame;

                    if (difference == 0)
                    {
                        // Replacement was successful
                        (replacementState_.State, predictState_.State) = (predictState_.State, replacementState_.State);
                        predictInput_ = input;
                        
                        lock (replacementMutex_)
                            if (currentReplacement_ <= replacementIndex)
                                activeReplacement_ = false; // Replacement has not been superseded yet. Return predict queue ownership to predict queue.
                        return;
                    }
                }

                if (difference < 0)
                {
                    logger_.Error("Predict state is behind replacement state. This should not happen. {Replacement} {Predict}", replacementState_.Frame, predictState_.Frame);
                    return;
                }
            
                // Replacement state is behind predict we need to update

                while (difference > 0)
                {
                    // This is assured to exist
                    TC localInput = ClientInputs[frame];

                    // Predict
                    PredictClientInput(input.ClientInput.Span, localInput);
                    ServerInputPredictor.PredictInput(ref input.ServerInput, replacementState_.State);

                    lock (replacementMutex_)
                    {
                        if (currentReplacement_ > replacementIndex)
                            return;

                        // If were still current, we may add our prediction

                        Memory<byte> serialized = MemoryPackSerializer.Serialize(input);
                        predictQueue_.Enqueue(serialized);
                    }

                    replacementState_.Update(input);
                    difference--;
                }
            }
        }
    }

    void PredictUpdate()
    {
        // Input
        TC localInput = InputProvider.GetInput();
        ClientInputs.Add(localInput);
        Memory<byte> input = MemoryPackSerializer.Serialize(localInput);
        long frame = predictState_.Frame; // This is ok, the value of Frame may be changed only by this method, which is synchronized by tickMutex
        
        Dispatcher.SendInput(frame, input);
            
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
            Memory<byte> predict = MemoryPackSerializer.Serialize(predictInput_);

            lock (replacementMutex_)
                if (!activeReplacement_) 
                    predictQueue_.Enqueue(predict);
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
        // TODO: what to do with terminated players?

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
