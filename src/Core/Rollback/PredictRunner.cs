using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Providers;
using Kcky.GameNewt.Transport;
using Kcky.GameNewt.Utility;
using Kcky.Useful;
using MemoryPack;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kcky.GameNewt.Client;

sealed class PredictRunner<TC, TS, TG>
    ( IClientInputProvider<TC> clientInputProvider, 
        IDisplayer<TG> displayer,
        IClientSender sender,
        UpdateInputPredictor<TC, TS, TG> predictor,
        IndexedQueue<TC> clientInputs,
        ReplacementReceiver<TC, TS, TG> replacementReceiver,
        ReplacementCoordinator coordinator,
        ILoggerFactory loggerFactory)
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly PooledBufferWriter<byte> predictInputWriter_ = new();
    readonly StateHolder<TC, TS, TG> predictHolder_ = new();
    UpdateInput<TC, TS> predictInput_ = UpdateInput<TC, TS>.Empty;

    readonly ILogger logger_ = loggerFactory.CreateLogger<PredictRunner<TC, TS, TG>>();

    readonly object frameLock_ = new();
    
    long frame_;
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

    public void Init(long frame, TG state)
    {

        Frame = frame;
        predictHolder_.Frame = frame;
        predictHolder_.State = state;
    }

    public void Update()
    {
        // Input
        TC localInput = clientInputProvider.GetInput();
        long frame = predictHolder_.Frame + 1;
        Frame = frame;

        lock (clientInputs)
        {
            long used = clientInputs.Add(localInput);
            Debug.Assert(frame == used);
        }

        // Send
        sender.SendInput(frame, localInput);

        replacementReceiver.TryReceive(frame, predictHolder_, ref predictInput_);

        logger_.LogTrace("Updating predict at frame {frame}.", frame);

        predictor.Predict(ref predictInput_, localInput, predictHolder_.State);
        
        // Update
        predictHolder_.Update(predictInput_);

        // Save prediction input if this timeline is not stale
        MemoryPackSerializer.Serialize(predictInputWriter_, predictInput_);
        coordinator.TryGivePredictionInput(predictInputWriter_);

        // Display
        displayer.AddPredict(frame, predictHolder_.State);
    }
}
