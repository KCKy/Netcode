using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FrameworkTest.Extensions;
using MemoryPack;

namespace FrameworkTest;

public sealed class Client<TPlayerInput, TServerInput, TGameState>
    where TGameState : IGameState<TPlayerInput, TServerInput>, new() where TPlayerInput : new()
{
    readonly IClientSession session_;

    readonly IPlayerInputProvider<TPlayerInput> provider_;

    readonly IInputPredictor<TPlayerInput, TServerInput, TGameState> predictor_;

    readonly GameInputQueue<ReadOnlyMemory<byte>> gameInputQueue_ = new();

    readonly PredictQueue<TPlayerInput, TServerInput> predictQueue_ = new();

    long id_ = -1;

    public long Id
    {
        get => id_;

        set
        {
            id_ = value;
            Displayer?.SetId(value);
        }
    }

    TGameState authoritativeState_ = new();
    TGameState currentState_ = new();

    public IClientDisplayer<TPlayerInput, TServerInput, TGameState>? Displayer { get; init; } = null;

    public Client(IClientSession session, IPlayerInputProvider<TPlayerInput> provider,
        IInputPredictor<TPlayerInput, TServerInput, TGameState> predictor)
    {
        session_ = session;
        provider_ = provider;
        predictor_ = predictor;
    }

    void HandleGameInput(long frame, ReadOnlyMemory<byte> data)
    {
        gameInputQueue_.AddInputFrame(data, frame);
    }

    readonly TaskCompletionSource initComplete_ = new();

    void HandleInitiate(long frame, ReadOnlyMemory<byte> data)
    {
        authFrame_ = frame;
        predictFrame_ = frame;
        gameInputQueue_.CurrentFrame = frame + 1;

        var state = MemoryPackSerializer.Deserialize<TGameState>(data.Span);

        if (state is null)
            throw new ArgumentException("Received invalid init state.", nameof(data));

        currentState_ = state;
        authoritativeState_ = state.MemoryPackCopy();

        Console.WriteLine($"Received init state for frame {frame}");

        initComplete_.SetResult();
    }

    readonly object predictMutex_ = new();

    TimeSpan frameTime_ = TimeSpan.FromSeconds(2f);

    readonly ClientInputs<TPlayerInput> clientInputs_ = new();

    void ReplaceInputInPredict(ref Input<TPlayerInput, TServerInput> predict, in TPlayerInput clientInput)
    {
        int length = predict.PlayerInputs.Length;
        for (int i = 0; i < length; i++)
        {
            if (predict.PlayerInputs[i].Id == Id)
            {
                predict.PlayerInputs[i].Input = clientInput;
                predict.PlayerInputs[i].Terminated = false;
                return;
            }
        }

        /*
        var replacement = new (long, TPlayerInput, bool)[length + 1];

        predict.PlayerInputs.CopyTo(replacement, 0);
        replacement[length] = new ()
        */

        // TODO: this expects from the user to create predict corretly
    }

    long authFrame_ = -1;
    long predictFrame_ = -1;

    TPlayerInput GainInputUnsafe(long frame)
    {
        if (!clientInputs_.TryGet(frame, out var clientInput))
        {
            clientInput = provider_.GetInput(frame);
            session_.SendInput(frame, MemoryPackSerializer.Serialize(clientInput));
            clientInputs_.AddNext(clientInput, frame);
        }

        return clientInput;
    }

    Input<TPlayerInput, TServerInput> currentPredictInput_ = new();

    async Task RunPredictAsync()
    {
        await Task.Yield();

        while (true)
        {
            // Frame rate
            Task nextFrame = frameTime_.Ticks == 0 ? Task.CompletedTask : Task.Delay(frameTime_);

            lock (predictMutex_)
            {
                // Gather user input and prediction

                predictFrame_++;

                currentPredictInput_ = predictor_.PredictInput(currentPredictInput_);

                var clientInput = GainInputUnsafe(predictFrame_);

                ReplaceInputInPredict(ref currentPredictInput_, clientInput);

                ReadOnlyMemory<byte> predictData = MemoryPackSerializer.Serialize(currentPredictInput_);

                predictQueue_.AddPredict(predictData, predictFrame_);

                // Take another step of predict simulation
                currentState_.Update(currentPredictInput_); 

                Displayer?.AddFrame(currentState_.MemoryPackCopy(), predictFrame_);
            }

            await nextFrame;
        }
    }

    void ReplacePredict(TGameState state, Input<TPlayerInput, TServerInput> currentInput, long frame, long goalFrame)
    {
        Stopwatch watch = new();
        watch.Start();

        PredictQueue<TPlayerInput, TServerInput> replacementQueue = new();

        while (true) // TODO: fail after finite steps
        {
            TPlayerInput clientInput;

            // TODO: this could be done a bit smarter

            lock (predictMutex_)
            {
                if (frame >= goalFrame /*&& frame >= predictFrame*/)
                {
                    currentPredictInput_ = currentInput;
                    currentState_ = state;
                    predictFrame_ = frame;
                    predictQueue_.ReplaceTimeline(replacementQueue);

                    watch.Stop();

                    Console.WriteLine($"Successfuly replaced timeline at {frame} in {watch.ElapsedMilliseconds} ms.");
                    Displayer?.AddFrame(state, frame);

                    return;
                }

                frame++;
                
                clientInput = GainInputUnsafe(frame);
            }

            currentInput = predictor_.PredictInput(currentInput);

            ReplaceInputInPredict(ref currentInput, clientInput);
            
            state.Update(currentInput);
            
            var predictData = MemoryPackSerializer.Serialize(currentInput);

            replacementQueue.AddPredict(predictData, frame);
        }
    }

    const long FixedPredictLength = 8;

    public async Task RunAsync()
    {
        session_.OnGameInput += HandleGameInput;
        session_.OnInitiate += HandleInitiate;

        Id = await session_.StartAsync();

        await initComplete_.Task;

        frameTime_ = TimeSpan.FromSeconds(1f / TGameState.DesiredTickRate);
        
        var copy = authoritativeState_.MemoryPackCopy();

        ReplacePredict(copy, predictor_.PredictInput(), authFrame_, authFrame_ + FixedPredictLength);

        RunPredictAsync().AssureSuccess();

        await foreach (var data in gameInputQueue_)
        {
            var input = MemoryPackSerializer
                .Deserialize<Input<TPlayerInput, TServerInput>>(data.Span); // TODO: this could be done beforehand

            authoritativeState_.Update(input);
            authFrame_++;

            // TODO: checksum

            bool predictionValid = predictQueue_.CheckDequeue(data, authFrame_);

            // Console.WriteLine($"Authorize frame {authFrame_}: {predictionValid}");
            
            if (!predictionValid)
                ReplacePredict(authoritativeState_.MemoryPackCopy(), input, authFrame_, authFrame_ + FixedPredictLength);
        }
    }
}
