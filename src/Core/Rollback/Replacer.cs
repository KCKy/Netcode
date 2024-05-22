﻿using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading.Tasks;
using Kcky.GameNewt.DataStructures;
using Kcky.GameNewt.Utility;
using MemoryPack;
using Kcky.Useful;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

sealed class Replacer<TC, TS, TG> where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly StateHolder<TC, TS, TG, ReplacementStateType> replacementHolder_;
    readonly PooledBufferWriter<byte> replacementInputWriter_ = new();
    readonly ILogger logger_;
    readonly StateHolder<TC, TS, TG, AuthoritativeStateType> authStateHolder_;
    readonly ReplacementCoordinator coordinator_;
    readonly IndexedQueue<TC> clientInputs_;
    readonly UpdateInputPredictor<TC, TS, TG> predictor_;
    readonly ReplacementReceiver<TC, TS, TG> receiver_;

    public Replacer(StateHolder<TC, TS, TG, AuthoritativeStateType> authStateHolder,
        ReplacementCoordinator coordinator,
        IndexedQueue<TC> clientInputs,
        UpdateInputPredictor<TC, TS, TG> predictor,
        ReplacementReceiver<TC, TS, TG> receiver,
        ILoggerFactory loggerFactory)
    {
        authStateHolder_ = authStateHolder;
        coordinator_ = coordinator;
        clientInputs_ = clientInputs;
        predictor_ = predictor;
        receiver_ = receiver;
        replacementHolder_ = new(loggerFactory);
        logger_ = loggerFactory.CreateLogger<Replacer<TC, TS, TG>>();
    }

    public void BeginReplacement(long frame, UpdateInput<TC, TS> input)
    {
        // Acquire index now, to assure correct ordering of replacements. 
        long index = coordinator_.AcquireReplacementIndex();

        // As this is called synchronously from auth state update this does not need to be synchronized.
        Debug.Assert(authStateHolder_.Frame == frame);
        var authState = authStateHolder_.GetSerialized();

        ReplaceGameStateAsync(index, frame, authState, input).AssureSuccess();
    }

    async Task ReplaceGameStateAsync(long replacementIndex, long frame, Memory<byte> serializedState, UpdateInput<TC, TS> input)
    {
        await Task.Yield(); // Run async. in a different thread

        logger_.LogDebug("Began replacement for frame {Frame}.", frame);

        // Wait for earlier replacements to finish
        lock (replacementHolder_)
        {
            if (!coordinator_.CheckReplacementCurrent(replacementIndex))
                return;

            replacementInputWriter_.Reset();
            
            lock (clientInputs_)
                clientInputs_.Pop(frame); // Only inputs greater than frame will be needed from now (replacements which could need it are finished).

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
                    logger_.LogWarning("Predict state is behind replacement state. This should happen only on startup. {Replacement} {Predict}", replFrame, replFrame + difference);
                    return;
            }

            // Replacement state is behind predict we need to update

            logger_.LogDebug("Need to catchup {Updates} updates.", difference);

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
            lock (clientInputs_)
                localInput = clientInputs_[frame]; // This is supposed to never fail

            // Predict
            predictor_.Predict(ref input, localInput, replacementHolder_.State);

            MemoryPackSerializer.Serialize(replacementInputWriter_, input);

            if (!coordinator_.TryGiveReplacementInput(replacementIndex, replacementInputWriter_))
                return false;

            replacementHolder_.Update(input);

            Debug.Assert(frame == replacementHolder_.Frame);
        }

        return true;
    }

    long TryReplace(long replacementIndex, long frame, UpdateInput<TC, TS> input)
    {
        Debug.Assert(frame == replacementHolder_.Frame);

        long difference = receiver_.TryGive(replacementHolder_, input);
        
        if (difference == 0)
        {
            coordinator_.FinishReplacement(replacementIndex);
            logger_.LogDebug("Successfully replaced predict at frame {Frame}.", frame);
        }

        return difference;
    }
}
