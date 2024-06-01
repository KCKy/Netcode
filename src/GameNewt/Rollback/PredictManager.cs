using System;
using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Dispatcher;
using Kcky.GameNewt.Utility;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

/// <summary>
/// Manages the prediction state using the rollback Netcode algorithm.
/// Keeps track of mispredictions, and when they occur, propagates a replacement prediction state.
/// </summary>
/// <typeparam name="TClientInput"></typeparam>
/// <typeparam name="TServerInput"></typeparam>
/// <typeparam name="TGameState"></typeparam>
sealed class PredictManager<TClientInput, TServerInput, TGameState> where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new()
{
    readonly StateHolder<TClientInput, TServerInput, TGameState, AuthoritativeStateType> authState_;
    readonly IClientSender sender_;
    readonly ILoggerFactory loggerFactory_;
    readonly HandleNewPredictiveStateDelegate<TGameState> newPredictiveStateCallback_;
    readonly PredictServerInputDelegate<TServerInput, TGameState> predictServerInput_;
    readonly PredictClientInputDelegate<TClientInput> predictClientInput_;
    readonly ProvideClientInputDelegate<TClientInput> provideClientInput_;
    readonly ILogger logger_;
    readonly IndexedQueue<TClientInput> clientInputs_ = new(); // This queue needs to be locked. Making new client inputs is exclusive to predict update.
    readonly ReplacementCoordinator coordinator_;

    PredictRunner<TClientInput, TServerInput, TGameState>? predictRunner_ = null;
    Replacer<TClientInput, TServerInput, TGameState>? replacer_= null;
    UpdateInputPredictor<TClientInput, TServerInput, TGameState>? predictor_ = null;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="authState">The authoritative state holder .</param>
    /// <param name="sender">Sender for sending inputs.</param>
    /// <param name="loggerFactory">The logger factory to use for logging.</param>
    /// <param name="newPredictiveStateCallback">Callback to invoke when new prediction is state has been calculated.</param>
    /// <param name="predictServerInput">Delegate which predicts server input.</param>
    /// <param name="predictClientInput">Delegate which predicts client input.</param>
    /// <param name="provideClientInput">Delegate which provides local client input.</param>
    public PredictManager(StateHolder<TClientInput, TServerInput, TGameState, AuthoritativeStateType> authState,
        IClientSender sender,
        ILoggerFactory loggerFactory,
        HandleNewPredictiveStateDelegate<TGameState> newPredictiveStateCallback,
        PredictServerInputDelegate<TServerInput, TGameState> predictServerInput,
        PredictClientInputDelegate<TClientInput> predictClientInput,
        ProvideClientInputDelegate<TClientInput> provideClientInput)
    {
        authState_ = authState;
        sender_ = sender;
        loggerFactory_ = loggerFactory;
        newPredictiveStateCallback_ = newPredictiveStateCallback;
        predictServerInput_ = predictServerInput;
        predictClientInput_ = predictClientInput;
        provideClientInput_ = provideClientInput;
        logger_ = loggerFactory.CreateLogger<PredictManager<TClientInput, TServerInput, TGameState>>();
        coordinator_ = new(loggerFactory);
    }

    /// <summary>
    /// The latest prediction frame.
    /// </summary>
    public long Frame => predictRunner_?.Frame ?? long.MinValue;

    /// <summary>
    /// Read only reference the current prediction frame.
    /// May be null, if not initiated.
    /// </summary>
    public TGameState? State => predictRunner_?.State;

    /// <summary>
    /// Initialize the predict manager to be able to receive inputs.
    /// </summary>
    /// <remarks>
    /// This shall be called exactly once before <see cref="InformAuthInput"/> or <see cref="Tick"/> or <see cref="CheckPredict"/> is called.
    /// </remarks>
    /// <param name="frame">The index of the state.</param>
    /// <param name="state">The state to initialize with.</param>
    /// <param name="localId">The local id of the client.</param>
    public void Init(long frame, TGameState state, int localId)
    {
        ReplacementReceiver<TClientInput, TServerInput, TGameState> receiver = new(loggerFactory_, frame);
        predictor_ = new(predictClientInput_, predictServerInput_, localId);
        predictRunner_ = new(provideClientInput_, newPredictiveStateCallback_, sender_, predictor_, clientInputs_, receiver, coordinator_, loggerFactory_, frame, state);
        replacer_ = new(authState_, coordinator_, clientInputs_, predictor_, receiver, loggerFactory_);
        
        lock (clientInputs_)
            clientInputs_.Set(frame + 1);

        logger_.LogDebug("Initiated predict state.");
    }

    /// <summary>
    /// Provide authoritative input for given frame update to check for mispredictions.
    /// </summary>
    /// <remarks>
    /// This shall be called atomically after given auth state update.
    /// </remarks>
    /// <param name="serializedInput">Borrow of serialized authoritative input.</param>
    /// <param name="frame">Index of the frame the input belongs to.</param>
    /// <param name="input">Move of input corresponding to <paramref name="serializedInput"/>.</param>
    public void InformAuthInput(ReadOnlySpan<byte> serializedInput, long frame, UpdateInput<TClientInput, TServerInput> input)
    {
        if (replacer_ is not { } replacer)
            return;

        if (!coordinator_.TryDequeuePredictInput(out var predictedInput))
        {
            predictedInput = Memory<byte>.Empty;
            logger_.LogDebug("The queue is empty.");
        }
        
        if (predictedInput.Span.SequenceEqual(serializedInput))
            return;

        logger_.LogDebug("Divergence appeared for frame {Frame}.", frame);

        replacer.BeginReplacement(frame, input);
    }
    
    /// <summary>
    /// Update the predict state once.
    /// </summary>
    public void Tick()
    {
        if (predictRunner_ is {} runner)
            runner.Update();
    }

    /// <summary>
    /// Check and swap predict state if a replacement occured.
    /// </summary>
    public void CheckPredict()
    {
        if (predictRunner_ is {} runner)
            runner.CheckPredict();
    }

    /// <summary>
    /// Stops the predict manager from further management.
    /// </summary>
    /// <remarks>
    /// This method is thread safe.
    /// </remarks>
    public void Stop() => coordinator_.Stop();
}
