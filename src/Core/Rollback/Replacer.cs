using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading.Tasks;
using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Utility;
using MemoryPack;
using Serilog;
using Kcky.Useful;

namespace Kcky.GameNewt.Client;

sealed class Replacer<TC, TS, TG>
    (StateHolder<TC, TS, TG> authStateHolder,
    ReplacementCoordinator coordinator,
    IndexedQueue<TC> clientInputs,
    UpdateInputPredictor<TC, TS, TG> predictor,
    ReplacementReceiver<TC, TS, TG> receiver)
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly StateHolder<TC, TS, TG> replacementHolder_ = new();
    readonly PooledBufferWriter<byte> replacementInputWriter_ = new();
    readonly ILogger logger_ = Log.ForContext<Replacer<TC, TS, TG>>();

    public void BeginReplacement(long frame, UpdateInput<TC, TS> input)
    {
        // Acquire index now, to assure correct ordering of replacements. 
        long index = coordinator.AcquireReplacementIndex();

        // As this is called synchronously from auth state update this does not need to be synchronized.
        Debug.Assert(authStateHolder.Frame == frame);
        var authState = authStateHolder.Serialize();

        ReplaceGameStateAsync(index, frame, authState, input).AssureSuccess();
    }

    async Task ReplaceGameStateAsync(long replacementIndex, long frame, Memory<byte> serializedState, UpdateInput<TC, TS> input)
    {
        await Task.Yield(); // Run async. in a different thread

        logger_.Debug("Began replacement for frame {Frame}.", frame);

        // Wait for earlier replacements to finish
        lock (replacementHolder_)
        {
            if (!coordinator.CheckReplacementCurrent(replacementIndex))
                return;

            replacementInputWriter_.Reset();
            
            lock (clientInputs)
                clientInputs.Pop(frame); // Only inputs greater than frame will be needed from now (replacements which could need it are finished).

            TG? state = replacementHolder_.State;

            MemoryPackSerializer.Deserialize(serializedState.Span, ref state);
            ArrayPool<byte>.Shared.Return(serializedState);

            // Prepare replacement state
            if (state is null)
                throw new ArgumentException("Failed to copy state.", nameof(serializedState));
            
            replacementHolder_.Frame = frame;
            replacementHolder_.State = state;

            ReplacementLoop(replacementIndex, frame, input);
        }
    }

    void ReplacementLoop(long replacementIndex, long frame, UpdateInput<TC, TS> input)
    {
        while (true)
        {
            // Check how many steps remain
            long difference = TryReplace(replacementIndex, frame, input);

            switch (difference)
            {
                case 0:
                    return; // Replacement was done successfully.
                case < 0:
                    long replFrame = replacementHolder_.Frame;
                    logger_.Warning("Predict state is behind replacement state. This should happen only on startup. {Replacement} {Predict}", replFrame, replFrame + difference);
                    return;
            }

            // Replacement state is behind predict we need to update

            logger_.Debug("Need to catchup {Updates} updates.", difference);

            bool success = UpdateReplacementState(replacementIndex, difference, ref frame, input);
            if (!success)
                return;
        }
    }

    bool UpdateReplacementState(long replacementIndex, long difference, ref long frame, UpdateInput<TC, TS> input)
    {
        while (difference > 0)
        {
            frame++;
            difference--;

            TC localInput;
            lock (clientInputs)
                localInput = clientInputs[frame]; // This is supposed to never fail

            // Predict
            predictor.Predict(ref input, localInput, replacementHolder_.State);

            MemoryPackSerializer.Serialize(replacementInputWriter_, input);

            if (!coordinator.TryGiveReplacementInput(replacementIndex, replacementInputWriter_))
                return false;

            replacementHolder_.Update(input);

            Debug.Assert(frame == replacementHolder_.Frame);
        }

        return true;
    }

    long TryReplace(long replacementIndex, long frame, UpdateInput<TC, TS> input)
    {
        Debug.Assert(frame == replacementHolder_.Frame);

        long difference = receiver.TryGive(replacementHolder_, input);
        
        if (difference == 0)
        {
            coordinator.FinishReplacement(replacementIndex);
            logger_.Debug("Successfully replaced predict at frame {Frame}.", frame);
        }

        return difference;
    }
}
