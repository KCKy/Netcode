using System;
using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Dispatcher;
using Kcky.GameNewt.Utility;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

sealed class PredictManager<TC, TS, TG> where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly StateHolder<TC, TS, TG, AuthoritativeStateType> authState_;
    readonly IClientSender sender_;
    readonly ILoggerFactory loggerFactory_;
    readonly HandleNewPredictiveStateDelegate<TG> newPredictiveStateCallback_;
    readonly PredictServerInputDelegate<TS, TG> predictServerInput_;
    readonly PredictClientInputDelegate<TC> predictClientInput_;
    readonly ProvideClientInputDelegate<TC> provideClientInput_;
    readonly ILogger logger_;
    readonly IndexedQueue<TC> clientInputs_ = new(); // This queue needs to be locked. Making new client inputs is exclusive to predict update.
    readonly ReplacementCoordinator coordinator_;

    PredictRunner<TC, TS, TG>? predictRunner_ = null;
    Replacer<TC, TS, TG>? replacer_= null;
    UpdateInputPredictor<TC, TS, TG>? predictor_ = null;

    public PredictManager(StateHolder<TC, TS, TG, AuthoritativeStateType> authState,
        IClientSender sender,
        ILoggerFactory loggerFactory,
        HandleNewPredictiveStateDelegate<TG> newPredictiveStateCallback,
        PredictServerInputDelegate<TS, TG> predictServerInput,
        PredictClientInputDelegate<TC> predictClientInput,
        ProvideClientInputDelegate<TC> provideClientInput)
    {
        authState_ = authState;
        sender_ = sender;
        loggerFactory_ = loggerFactory;
        newPredictiveStateCallback_ = newPredictiveStateCallback;
        predictServerInput_ = predictServerInput;
        predictClientInput_ = predictClientInput;
        provideClientInput_ = provideClientInput;
        logger_ = loggerFactory.CreateLogger<PredictManager<TC, TS, TG>>();
        coordinator_ = new(loggerFactory);
    }

    public long Frame => predictRunner_?.Frame ?? long.MinValue;
    public TG? State => predictRunner_?.State;

    /// <summary>
    /// Initialize the predict manager to be able to receive inputs.
    /// </summary>
    /// <remarks>
    /// This shall be called exactly once before <see cref="InformAuthInput"/> or <see cref="Tick"/> is called.
    /// </remarks>
    /// <param name="frame">The index of the state.</param>
    /// <param name="state">The state to initialize with.</param>
    /// <param name="localId">The local id of the client.</param>
    public void Init(long frame, TG state, int localId)
    {
        ReplacementReceiver<TC, TS, TG> receiver = new(loggerFactory_, frame);
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
    public void InformAuthInput(ReadOnlySpan<byte> serializedInput, long frame, UpdateInput<TC, TS> input)
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
