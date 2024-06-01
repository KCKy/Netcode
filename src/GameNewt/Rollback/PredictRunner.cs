using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Utility;
using Kcky.Useful;
using MemoryPack;
using System.Diagnostics;
using Kcky.GameNewt.Dispatcher;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

/// <summary>
/// Continuously updates the prediction state from local inputs.
/// Receives replacements if available.
/// </summary>
/// <typeparam name="TClientInput"></typeparam>
/// <typeparam name="TServerInput"></typeparam>
/// <typeparam name="TGameState"></typeparam>
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

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="provideClientInput">Delegate to provide local client input.</param>
    /// <param name="predictiveStateCallback">Callback to invoke when new prediction is state has been calculated.</param>
    /// <param name="sender">Sender for sending inputs.</param>
    /// <param name="predictor">Predictor to predict next frame inputs.</param>
    /// <param name="clientInputs">Queue of local client inputs.</param>
    /// <param name="replacementReceiver">The receiver instance to check for finished replacements.</param>
    /// <param name="coordinator">The replacement coordinator to provide predicted inputs to.</param>
    /// <param name="loggerFactory">Logger factory to use for logging.</param>
    /// <param name="frame">The frame runner should start at.</param>
    /// <param name="state">The start state the <see cref="frame"/> corresponds to.</param>
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

    /// <summary>
    /// The current frame number of the held prediction state.
    /// </summary>
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

    /// <summary>
    /// The current prediction state.
    /// </summary>
    public TGameState State => predictHolder_.State;

    /// <summary>
    /// Updates predict if replacement available in <see cref="ReplacementReceiver{TClientInput,TServerInput,TGameState}"/>.
    /// </summary>
    public void CheckPredict()
    {
        if (replacementReceiver_.TryReceive(predictHolder_.Frame, predictHolder_, ref predictInput_))
            predictiveStateCallback_(predictHolder_.Frame, predictHolder_.State);
    }

    /// <summary>
    /// Collect new inputs and do a next frame step.
    /// </summary>
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

        // Receiving here is important, because otherwise we could leave an old, unused replacement in there.
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
