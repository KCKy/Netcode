﻿using Core.DataStructures;
using Core.Extensions;
using Core.Transport;
using Core.Providers;
using Core.Utility;
using MemoryPack;
using Serilog;

namespace Core.Client;

public sealed class Client<TC, TS, TG> : IClientSession
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly IStateHolder<TC, TS, TG> authStateHolder_;
    readonly ILogger logger_ = Log.ForContext<Client<TC, TS, TG>>();
    readonly ISpeedController clock_;
    readonly IDisplayer<TG> displayer_;
    readonly IClientDispatcher dispatcher_;
    readonly IPredictManager<TC, TS, TG> predictManager_;

    readonly CancellationTokenSource clockCancellation_ = new();
    readonly object terminationMutex_ = new();
    readonly UpdateTimer timer_;

    bool started_ = false;
    bool terminated_ = false;
    bool initiated_ = false;
    bool identified_ = false;

    public double PredictDelayMargin
    {
        get => clock_.TargetDelta;
        set => clock_.TargetDelta = value;
    }
    
    public Client(IClientDispatcher dispatcher,
        IDisplayer<TG>? displayer,
        IClientInputProvider<TC>? inputProvider,
        IServerInputPredictor<TS, TG>? serverInputPredictor = null,
        IClientInputPredictor<TC>? clientInputPredictor = null)
    {
        dispatcher_ = dispatcher;
        displayer_ = displayer ?? new DefaultDisplayer<TG>();
        clock_ = new BasicSpeedController()
        {
            TargetTPS = TG.DesiredTickRate
        };

        authStateHolder_ = new StateHolder<TC, TS, TG>();

        predictManager_ = new PredictManager<TC, TS, TG>
        {
            ClientInputs = new LocalInputQueue<TC>(),
            InputPredictor = clientInputPredictor ?? new DefaultClientInputPredictor<TC>(),
            ServerInputPredictor = serverInputPredictor ?? new DefaultServerInputPredictor<TS, TG>(),
            InputProvider = inputProvider ?? new DefaultClientInputProvider<TC>(),
            AuthState = authStateHolder_,
            Dispatcher = dispatcher_,
            Displayer = displayer_
        };

        PredictDelayMargin = 0.15f;
        timer_ = new(logger_);
    }

    internal Client(IClientDispatcher dispatcher, IDisplayer<TG> displayer, ISpeedController controller, IStateHolder<TC, TS, TG> authStateHolder,
        IPredictManager<TC, TS, TG> predictManager)
    {
        dispatcher_ = dispatcher;
        displayer_ = displayer;
        clock_ = controller;
        authStateHolder_ = authStateHolder;
        predictManager_ = predictManager;
        PredictDelayMargin = 0.15f;
        timer_ = new(logger_);
    }

    public long Id { get; private set; } = long.MaxValue;

    public bool TraceState { get; set; }
    public bool UseChecksum { get; set; }

    // TODO: this could break the stopwatch, even for server
    public bool TraceFrameTime { get; set; }

    public async Task RunAsync()
    {
        lock (terminationMutex_)
        {
            if (started_ || terminated_)
                return;

            started_ = true;
        }

        logger_.Information("The client is starting.");

        try
        {
            await clock_.RunAsync(clockCancellation_.Token);
        }
        catch (OperationCanceledException) { }
    }

    public void Terminate()
    {
        lock (terminationMutex_)
        {
            if (terminated_)
                return;

            logger_.Information("The client is being terminated.");

            terminated_ = true;

            dispatcher_.Disconnect();

            predictManager_.Stop();
            clock_.OnTick -= predictManager_.Tick;
            clockCancellation_.Cancel();
        }
    }

    void AssertChecksum(long? checksum)
    {
        if (!UseChecksum || checksum is not { } check)
            return;
        
        var serialized = Memory<byte>.Empty;
        if (TraceState)
            serialized = authStateHolder_.Serialize();

        long actual = authStateHolder_.GetChecksum();
        if (actual != check)
        {
            logger_.Fatal("Desync detected {ActualSum} != {ExpectedSum} - {State}.", actual, check, serialized);
            throw new InvalidOperationException("The auth state has diverged from the server.");
        }

        serialized.ReturnToArrayPool();
    }

    void Update(Memory<byte> serializedInput, long? checksum, long inputFrame)
    {
        // TODO: maybe pool?
        var input = MemoryPackSerializer.Deserialize<UpdateInput<TC, TS>>(serializedInput.Span);

        lock (authStateHolder_)
        {
            authStateHolder_.Update(input);
            long frame = authStateHolder_.Frame;

            if (frame != inputFrame)
            {
                logger_.Fatal("Given invalid input of frame {Frame} for auth update {Index}.", inputFrame, frame);
                throw new ArgumentException("Given invalid frame for auth update.", nameof(inputFrame));
            }

            AssertChecksum(checksum);
            displayer_.AddAuthoritative(frame, authStateHolder_.State);
            logger_.Verbose("Authorized frame {Frame}", frame);
            predictManager_.InformAuthInput(serializedInput, frame, input);
        }

        serializedInput.ReturnToArrayPool();
    }

    void Authorize(Memory<byte> input, long? checksum, long frame)
    {
        if (TraceFrameTime)
            timer_.Start();
             
        Update(input, checksum, frame);

        if (TraceFrameTime)
            timer_.End(frame);
    }

    void IClientSession.Start(long id)
    {
        lock (terminationMutex_)
        {
            if (identified_ || terminated_ || !started_)
                return;

            logger_.Debug("Client received id {Id}", id);

            identified_ = true;
            Id = id;
            predictManager_.LocalId = id;
            displayer_.Init(id);

            if (initiated_ && identified_)
            {
                clock_.OnTick += predictManager_.Tick;
            }
        }
    }

    void IClientSession.Initialize(long frame, Memory<byte> serializedState)
    {
        lock (terminationMutex_)
        {
            if (initiated_ || terminated_ || !started_)
                return;

            initiated_ = true;

            // Deserialize state
            var span = serializedState.Span;
            var authState = MemoryPackSerializer.Deserialize<TG>(span);
            var predictState = MemoryPackSerializer.Deserialize<TG>(span);
            
            if (authState is null || predictState is null)
            {
                logger_.Fatal("Received invalid init state {Serialized}.", serializedState);
                throw new ArgumentException("Received invalid init state.", nameof(serializedState));
            }

            logger_.Debug("Received init state for {Frame} with {Serialized}.", frame, serializedState);

            serializedState.ReturnToArrayPool();

            // Init auth, no need to lock
            authStateHolder_.Frame = frame;
            authStateHolder_.State = authState;
            displayer_.AddAuthoritative(frame, authState);

            // Init predict
            predictManager_.Init(frame, predictState);
            
            // Run
            if (initiated_ && identified_)
                clock_.OnTick += predictManager_.Tick;
        }
    }

    void IClientSession.Finish()
    {
        logger_.Information("The client has finished.");
        Terminate();
    }

    void IClientSession.AddAuthoritativeInput(long frame, Memory<byte> input, long? checksum)
    {
        logger_.Verbose("Received auth input for frame {Frame} with checksum {CheckSum}.", frame, checksum);
        Authorize(input, checksum, frame);
    }

    void IClientSession.SetDelay(double delay) => clock_.CurrentDelta = delay;
}
