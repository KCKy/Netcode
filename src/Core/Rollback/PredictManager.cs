using System;
using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Providers;
using Kcky.GameNewt.Transport;
using Kcky.GameNewt.Utility;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

sealed class PredictManager<TC, TS, TG>(StateHolder<TC, TS, TG> authState, IClientSender sender, ILoggerFactory loggerFactory)
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly ILogger logger_ = loggerFactory.CreateLogger<PredictManager<TC, TS, TG>>();

    readonly IndexedQueue<TC> clientInputs_ = new(); // This queue needs to be locked. Making new client inputs is exclusive to predict update.

    readonly ReplacementCoordinator coordinator_ = new();

    PredictRunner<TC, TS, TG>? predictRunner_ = null;
    Replacer<TC, TS, TG>? replacer_= null;
    UpdateInputPredictor<TC, TS, TG>? predictor_ = null;

    readonly object tickLock_ = new();

    public int LocalId
    {
        set
        {
            var predictor = predictor_ ?? throw new InvalidOperationException();
            predictor.LocalId = value;
        }
    }

    public IServerInputPredictor<TS, TG> ServerInputPredictor { private get; set; } = new DefaultServerInputPredictor<TS, TG>();
    public IClientInputPredictor<TC> ClientInputPredictor { private get; set; } = new DefaultClientInputPredictor<TC>();
    public IClientInputProvider<TC> ClientInputProvider { private get; set; } = new DefaultClientInputProvider<TC>();
    public IDisplayer<TG> Displayer { private get; set; } = new DefaultDisplayer<TG>();

    /// <summary>
    /// Initialize the predict manager to be able to receive inputs.
    /// </summary>
    /// <remarks>
    /// This shall be called exactly once before <see cref="InformAuthInput"/> or <see cref="Tick"/> is called.
    /// </remarks>
    /// <param name="frame">The index of the state.</param>
    /// <param name="state">The state to initialize with.</param>
    public void Init(long frame, TG state)
    {
        ReplacementReceiver<TC, TS, TG> receiver = new();
        predictor_ = new(ClientInputPredictor, ServerInputPredictor);
        predictRunner_ = new(ClientInputProvider, Displayer, sender, predictor_, clientInputs_, receiver, coordinator_, loggerFactory);
        replacer_ = new(authState, coordinator_, clientInputs_, predictor_, receiver);

        receiver.Init(frame);
        coordinator_.Init();
        predictRunner_.Init(frame, state);
        
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
        lock (tickLock_)
            if (predictRunner_ is {} runner)
                runner.Update();
    }

    public long Frame => predictRunner_?.Frame ?? -1;
    public TG? State => predictRunner_?.State;

    /// <summary>
    /// Stops the predict manager from further management.
    /// </summary>
    /// <remarks>
    /// This method is thread safe.
    /// </remarks>
    public void Stop() => coordinator_.Stop();
}
