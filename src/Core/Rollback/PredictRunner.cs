using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Utility;
using Kcky.Useful;
using MemoryPack;
using System.Diagnostics;
using Kcky.GameNewt.Dispatcher;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

sealed class PredictRunner<TC, TS, TG> where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly PooledBufferWriter<byte> predictInputWriter_ = new();
    readonly StateHolder<TC, TS, TG, PredictiveStateType> predictHolder_;
    readonly ILogger logger_;
    readonly object frameLock_ = new();
    readonly ProvideClientInputDelegate<TC> provideClientInput_;
    readonly HandleNewPredictiveStateDelegate<TG> predictiveStateCallback_;
    readonly IClientSender sender_;
    readonly UpdateInputPredictor<TC, TS, TG> predictor_;
    readonly IndexedQueue<TC> clientInputs_;
    readonly ReplacementReceiver<TC, TS, TG> replacementReceiver_;
    readonly ReplacementCoordinator coordinator_;
    UpdateInput<TC, TS> predictInput_ = UpdateInput<TC, TS>.Empty;
    long frame_;

    public PredictRunner(ProvideClientInputDelegate<TC> provideClientInput,
        HandleNewPredictiveStateDelegate<TG> predictiveStateCallback,
        IClientSender sender,
        UpdateInputPredictor<TC, TS, TG> predictor,
        IndexedQueue<TC> clientInputs,
        ReplacementReceiver<TC, TS, TG> replacementReceiver,
        ReplacementCoordinator coordinator,
        ILoggerFactory loggerFactory, long frame, TG state)
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
        logger_ = loggerFactory.CreateLogger<PredictRunner<TC, TS, TG>>();
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

    public TG State => predictHolder_.State;

    public void CheckPredict()
    {
        replacementReceiver_.TryReceive(predictHolder_.Frame, predictHolder_, ref predictInput_);
    }

    public void Update()
    {
        // Input
        TC localInput = provideClientInput_();
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
