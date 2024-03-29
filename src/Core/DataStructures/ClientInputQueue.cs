﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Core.Providers;
using Serilog;

namespace Core.DataStructures;

/// <summary>
/// Event describing the authorization of a given client's input.
/// If <paramref name="difference"/> is negative that means given input was not received in time and the server ignored the late input,
/// positive value means the input was received in-time to be accounted for in the frame update.
/// </summary>
/// <param name="id">The ID of the client the input belongs to.</param>
/// <param name="frame">The frame index of the frame the input is for.</param>
/// <param name="difference">The difference of the corresponding frame update time and the input receive time.</param>
public delegate void InputAuthoredDelegate(long id, long frame, TimeSpan difference);

/// <summary>
/// Receives all client input to the server. Constructs authoritative client update inputs <see cref="UpdateClientInfo{TClientInput}"/>.
/// Authorizes all received inputs and raises <see cref="inputAuthored_"/> informing whether inputs are being received on time.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
public sealed class ClientInputQueue<TClientInput>
where TClientInput : class, new()
{
    sealed class SingleClientQueue
    {
        public long LastAuthorizedInput = long.MinValue;
        readonly IClientInputPredictor<TClientInput> predictor_;
        readonly Dictionary<long, (TClientInput input, long timestamp)> frameToInput_ = new();
        TClientInput previousInput_ = new();

        public SingleClientQueue(IClientInputPredictor<TClientInput> predictor)
        {
            predictor_ = predictor;
        }

        public bool TryAdd(long frame, TClientInput input, long timestamp) => frameToInput_.TryAdd(frame, (input, timestamp));

        public long? WriteUpdateInfo(long frame, ref UpdateClientInfo<TClientInput> info)
        {
            long? time;

            if (frameToInput_.Remove(frame, out (TClientInput input, long timestamp) rec))
            {
                previousInput_ = rec.input;
                time = rec.timestamp;
            }
            else
            {
                predictor_.PredictInput(ref previousInput_);
                time = null;
            }

            info.Input = previousInput_;
            return time;
        }
    }

    readonly Dictionary<long, SingleClientQueue> idToInputs_ = new();
    readonly List<long> removedClients_ = new();

    readonly double ticksPerSecond_;
    readonly IClientInputPredictor<TClientInput> predictor_;

    readonly object mutex_ = new();

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="tps">The TPS the game should run at. Used for input delay calculations.</param>
    /// <param name="predictor">Input predictor for client inputs. Used as a substitute when input of a client are not received in time.</param>
    /// <param name="onInputAuthored">Raised when given client input is being authored.</param>
    public ClientInputQueue(double tps, IClientInputPredictor<TClientInput> predictor, InputAuthoredDelegate onInputAuthored)
    {
        ticksPerSecond_ = tps;
        predictor_ = predictor;
        inputAuthored_ = onInputAuthored;
    }

    readonly InputAuthoredDelegate inputAuthored_;

    /// <summary>
    /// The latest constructed input frame number.
    /// </summary>
    public long Frame
    {
        get
        {
            lock (mutex_)
                return frame_;
        }
    }

    long frame_ = -1;

    readonly ILogger logger_ = Log.ForContext<ClientInputQueue<TClientInput>>();

    /// <summary>
    /// Add a client, which is from now expected to send input.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <exception cref="ArgumentException">If client with given id has already been added.</exception>
    public void AddClient(long id)
    {
        lock (mutex_)
        {
            if (!idToInputs_.TryAdd(id, new(predictor_)))
            {
                logger_.Fatal("To add duplicate client {Id}", id);
                throw new ArgumentException("Client with given id is already present.", nameof(id));
            }

            logger_.Verbose("Added client {Id}.", id);
        }
    }

    /// <summary>
    /// Removes a client from the queue, discard all unretrieved inputs and disconnect the client.
    /// </summary>
    /// <param name="id">The id of the client.</param>
    /// <exception cref="ArgumentException">If the client id is not in the collection.</exception>
    public void RemoveClient(long id)
    {
        lock (mutex_)
        {

            if (!idToInputs_.Remove(id))
            {
                logger_.Fatal("To remove non-contained {Id}", id);
                throw new ArgumentException("Client with given id is already present.", nameof(id));
            }

            removedClients_.Add(id);

            logger_.Verbose("Removed client {Id}.", id);
        }
    }

    /// <summary>
    /// Adds an input for given client.
    /// </summary>
    /// <param name="id">ID of the client.</param>
    /// <param name="frame">Frame number to which the input corresponds.</param>
    /// <param name="input">The input.</param>
    public void AddInput(long id, long frame, TClientInput input)
    {
        lock (mutex_)
        {
            long now = Stopwatch.GetTimestamp();

            if (!idToInputs_.TryGetValue(id, out var clientInfo))
            {
                logger_.Debug("Got input from terminated client {Id} for {Frame} at {Current}..", id, frame, frame_);
                return;
            }

            if (frame <= frame_)
            {
                if (frame <= clientInfo.LastAuthorizedInput)
                    return; // No need to notify, notification has already been made.

                clientInfo.LastAuthorizedInput = frame;

                var framePart = Stopwatch.GetElapsedTime(lastFrameUpdate_, now);

                TimeSpan difference = TimeSpan.FromSeconds((frame - frame_) / ticksPerSecond_) - framePart;
                inputAuthored_?.Invoke(id, frame, difference);

                logger_.Debug( "Got late input from client {Id} for {Frame} at {Current} ({Time:F2} ms).", id, frame, frame_, difference.TotalMilliseconds);
                return;
            }
            
            if (!clientInfo.TryAdd(frame, input, now))
                return; // The input was already received.

            logger_.Verbose("Got input from client {Id} for {Frame} at {Current}.", id, frame, frame_);
        }
    }

    long lastFrameUpdate_ = long.MaxValue;

    /// <summary>
    /// Constructs the next input frame out of collected inputs, any late inputs will be ignored.
    /// </summary>
    /// <returns>Input frame for current frame.</returns>
    public Memory<UpdateClientInfo<TClientInput>> ConstructAuthoritativeFrame()
    {
        lock (mutex_)
        {
            lastFrameUpdate_ = Stopwatch.GetTimestamp();
            
            int length = idToInputs_.Count + removedClients_.Count;

            Memory<UpdateClientInfo<TClientInput>> frame = new UpdateClientInfo<TClientInput>[length];

            long nextFrame = frame_ + 1;

            var span = frame.Span;

            int i = 0;
            foreach ((long id, SingleClientQueue queue) in idToInputs_)
            {
                long? timestamp = queue.WriteUpdateInfo(nextFrame, ref span[i]);
                span[i].Id = id;

                if (timestamp is { } value)
                {
                    queue.LastAuthorizedInput = nextFrame;
                    TimeSpan difference = Stopwatch.GetElapsedTime(value, lastFrameUpdate_);
                    inputAuthored_?.Invoke(id, nextFrame, difference);

                    logger_.Verbose("Input from {Id} received {Time:F2} ms in advance.", id, difference.TotalMilliseconds);
                }
                
                i++;
            }

            foreach (long id in removedClients_)
            {
                span[i] = new(id, new(), true);
                i++;
            }
            
            frame_ = nextFrame;
            removedClients_.Clear();

            logger_.Verbose("Constructed authoritative frame for {FrameIndex}.", nextFrame);

            return frame;
        }
    }
}
