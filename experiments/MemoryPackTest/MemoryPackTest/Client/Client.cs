using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

    public long Id { get; private set; } = -1;

    TGameState authoritativeState_ = new();
    TGameState currentState_ = new();

    public IClientDisplayer<TPlayerInput, TServerInput, TGameState>? Displayer { get; set; } = null;

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

    async Task RunPredictAsync()
    {
        await Task.Yield();

        while (true)
        {
            TGameState stateCopy;

            // Frame rate
            Task nextFrame = frameTime_.Ticks == 0 ? Task.CompletedTask : Task.Delay(frameTime_);

            lock (predictMutex_)
            {
                // Gather user input and prediction
                var predict = predictor_.PredictInput(currentState_.MemoryPackCopy()); // TODO: copy!?
                var clientInput = provider_.GetInput();

                ReplaceInputInPredict(ref predict, clientInput);
                
                ReadOnlyMemory<byte> predictData = MemoryPackSerializer.Serialize(predict);

                predictFrame_++;

                clientInputs_.AddNext(clientInput, predictFrame_);
                predictQueue_.AddPredict(predictData, predictFrame_);
                
                session_.SendInput(predictFrame_, MemoryPackSerializer.Serialize(clientInput));

                // Take another step of predict simulation
                currentState_.Update(predict); 

                stateCopy = currentState_.MemoryPackCopy(); // TODO: some of this could be outside the critical section
            }

            Displayer?.AddFrame(stateCopy, predictFrame_);

            await nextFrame;
        }
    }

    void ReplacePredict(TGameState state, long frame)
    {
        PredictQueue<TPlayerInput, TServerInput> replacementQueue = new();
        
        while (true) // TODO: fail after finite steps
        {
            TPlayerInput clientInput;

            // TODO: this could be done a bit smarter

            lock (predictMutex_)
            {
                Debug.Assert(predictFrame_ <= frame);

                // Try to replace

                if (predictFrame_ == frame)
                {
                    currentState_ = state;
                    predictQueue_.ReplaceTimeline(replacementQueue);

                    Console.WriteLine($"Successfuly replaced timeline at {frame}");
                    Displayer?.AddFrame(state, frame);

                    return;
                }

                if (!clientInputs_.TryGet(frame + 1, out clientInput))
                    throw new Exception();
            }

            var stateCopy = state.MemoryPackCopy();

            var predict = predictor_.PredictInput(stateCopy); // TODO: consider what information to provide the predictor

            state.Update(predict);
            frame++;

            ReplaceInputInPredict(ref predict, clientInput);

            var predictData = MemoryPackSerializer.Serialize(predict);

            replacementQueue.AddPredict(predictData, frame);
        }
    }

    public async Task RunAsync()
    {
        session_.OnGameInput += HandleGameInput;
        session_.OnInitiate += HandleInitiate;

        Id = await session_.StartAsync();

        await initComplete_.Task;

        frameTime_ = TimeSpan.FromSeconds(1f / TGameState.DesiredTickRate);

        RunPredictAsync().AssureSuccess();

        await foreach (var data in gameInputQueue_)
        {
            var input = MemoryPackSerializer
                .Deserialize<Input<TPlayerInput, TServerInput>>(data.Span); // TODO: this could be done beforehand

            authoritativeState_.Update(input);
            authFrame_++;

            TGameState copy = authoritativeState_.MemoryPackCopy(); // TODO: checksum

            clientInputs_.ClearFrame(authFrame_);
            bool predictionValid = predictQueue_.CheckDequeue(data, authFrame_);

            if (!predictionValid)
            {
                ReplacePredict(copy, authFrame_);
            }
        }
    }
}
