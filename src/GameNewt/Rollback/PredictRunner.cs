using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Utility;
using Kcky.Useful;
using MemoryPack;
using System.Diagnostics;
using Kcky.GameNewt.Dispatcher;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

sealed class PredictRunner<TClientInput, TServerInput, TGameState> where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new()
{
    readonly PooledBufferWriter<byte> predictInputWriter_ = new();
    readonly StateHolder<TClientInput, TServerInput, TGameState, PredictiveStateType> predictHolder_;
    readonly ILogger logger_;
    readonly object frameLock_ = new();
    readonly ProvideClientInputDelegate<TClientInput> provideClientInput_;
    readonly HandleNewPredictiveStateDelegate<TGameState> predictiveStateCallback_;
    readonly IClientSender sender_;
    readonly UpdateInputPredictor<TClientInput, TServerInput, TGameState> predictor_;
    readonly IndexedQueue<TClientInput> clientInputs_;
    readonly ReplacementReceiver<TClientInput, TServerInput, TGameState> replacementReceiver_;
    readonly ReplacementCoordinator coordinator_;
    UpdateInput<TClientInput, TServerInput> predictInput_ = UpdateInput<TClientInput, TServerInput>.Empty;
    long frame_;

    public PredictRunner(ProvideClientInputDelegate<TClientInput> provideClientInput,
        HandleNewPredictiveStateDelegate<TGameState> predictiveStateCallback,
        IClientSender sender,
        UpdateInputPredictor<TClientInput, TServerInput, TGameState> predictor,
        IndexedQueue<TClientInput> clientInputs,
        ReplacementReceiver<TClientInput, TServerInput, TGameState> replacementReceiver,
        ReplacementCoordinator coordinator,
        ILoggerFactory loggerFactory, long frame, TGameState state)
    {
        provideClientInput_ = provideClientInput;
        predictiveStateCallback_ = predictiveStateCallback;
        sender_ = sender;
        predictor_ = predictor;
        clientInputs_ = clientInputs;
        replacementReceiver_ = replacementReceiver;
        coordinator_ = coordinator;
        predictHolder_ = new(loggerFactory)
        {
            Frame = frame,
            State = state
        };
        logger_ = loggerFactory.CreateLogger<PredictRunner<TClientInput, TServerInput, TGameState>>();
    }

    public long Frame
    {
        get
        {
            lock (frameLock_)
                return frame_;
        }
        private set
        {
            lock (frameLock_)
                frame_ = value;
        }
    }

    public TGameState State => predictHolder_.State;

    public void CheckPredict()
    {
        if (replacementReceiver_.TryReceive(predictHolder_.Frame, predictHolder_, ref predictInput_))
            predictiveStateCallback_(predictHolder_.Frame, predictHolder_.State);
    }

    public void Update()
    {
        // Input
        TClientInput localInput = provideClientInput_();
        long frame = predictHolder_.Frame + 1;
        Frame = frame;

        lock (clientInputs_)
        {
            long used = clientInputs_.Add(localInput);
            Debug.Assert(frame == used);
        }

        // Send
        sender_.SendInput(frame, localInput);

        replacementReceiver_.TryReceive(frame, predictHolder_, ref predictInput_);

        logger_.LogTrace("Updating predict at frame {frame}.", frame);

        predictor_.Predict(ref predictInput_, localInput, predictHolder_.State);
        
        // Update
        predictHolder_.Update(predictInput_);

        // Save prediction input if this timeline is not stale
        MemoryPackSerializer.Serialize(predictInputWriter_, predictInput_);
        coordinator_.TryGivePredictionInput(predictInputWriter_);

        // Display
        predictiveStateCallback_(frame, predictHolder_.State);
    }
}
