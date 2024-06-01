using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Kcky.Useful;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

/// <summary>
/// Coordinates the rollback netcode algorithm.
/// Assures there is only one replacement running at a time.
/// Keeps track of predicted inputs ina  predicted input queue from the current timeline to check for mispredictions.
/// </summary>
sealed class ReplacementCoordinator
{
    readonly object replacementMutex_ = new();
    readonly ConcurrentQueue<Memory<byte>> predictQueue_ = new();
    readonly ILogger logger_;

    bool activeReplacement_ = false;
    long currentReplacement_ = long.MinValue;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to user for logging.</param>
    public ReplacementCoordinator(ILoggerFactory loggerFactory)
    {
        logger_ = loggerFactory.CreateLogger<ReplacementCoordinator>();
    }

    /// <summary>
    /// Tries get the oldest predicted input of this prediction timeline from the predicted input queue.
    /// Useful when a new auth state update occurs to check whether a misprediction occured.
    /// If the input match with the ones used for the authoritative simulation, there has been no misprediction.
    /// If a replacement is running, returns from the queue of inputs of the current replacement,
    /// otherwise returns from a queue of inputs which were used to calculate prediction state.
    /// </summary>
    /// <param name="input">The oldest predicted input of the current timeline.</param>
    /// <returns>Whether there is an input in the queue.</returns>
    /// <remarks>This is thread-safe.</remarks>
    public bool TryDequeuePredictInput(out Memory<byte> input) => predictQueue_.TryDequeue(out input);

    /// <summary>
    /// Registers that a new replacement shall begin and gives it and identification index.
    /// </summary>
    /// <returns>The index of the new replacement.</returns>
    /// <remarks>This is thread-safe and atomic.</remarks>
    public long AcquireReplacementIndex()
    {
        lock (replacementMutex_)
        {
            long index = ++currentReplacement_;
            activeReplacement_ = true; // Disable predict input generation
            predictQueue_.Clear();
            return index;
        }
    }

    /// <summary>
    /// Signals that a replacement successfully finished.
    /// Does not need to be called when the replacement has been succeeded by a newer replacement.
    /// </summary>
    /// <param name="index">The index of the successfully finished replacement.</param>
    /// <remarks>This is thread-safe and atomic.</remarks>
    public void FinishReplacement(long index)
    {
        lock (replacementMutex_)
        {
            Debug.Assert(currentReplacement_ >= index);
            if (currentReplacement_ == index)
                activeReplacement_ = false;
            logger_.LogDebug("Replacement finished (A:{Active})", activeReplacement_);
        }
    }

    /// <summary>
    /// Checks whether there is not a newer replacement than the one with <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index of the replacement to check staleness off.</param>
    /// <returns>Whether the replacement with <paramref name="index"/> is the latest one.</returns>
    /// <remarks>This is thread-safe and atomic.</remarks>
    public bool CheckReplacementCurrent(long index)
    {
        lock (replacementMutex_)
            return currentReplacement_ <= index;
    }

    /// <summary>
    /// Signals that the rollback netcode algorithm is over.
    /// All new replacements are to be created cancelled.
    /// </summary>
    public void Stop()
    {
        lock (replacementMutex_)
            currentReplacement_ = long.MaxValue;
    }

    /// <summary>
    /// Tries to give next predicted input from the currently running replacement to the predicted input queue.
    /// </summary>
    /// <param name="index">The index of the replacement.</param>
    /// <param name="writer">The writer holding the serialized input.</param>
    /// <returns>Whether this replacement has been succeeded and shall stop early.</returns>
    public bool TryGiveReplacementInput(long index, PooledBufferWriter<byte> writer)
    {
        lock (replacementMutex_)
        {
            if (currentReplacement_ > index)
            {
                writer.Reset();
                return false;
            }
            
            predictQueue_.Enqueue(writer.ExtractAndReplace());
            return true;
        }
    }

    /// <summary>
    /// Tries to give next predicted input of the prediction simulation to the predicted input queue.
    /// If a replacement is running the input is ignored.
    /// </summary>
    /// <param name="writer">The writer to take the input from.</param>
    public void TryGivePredictionInput(PooledBufferWriter<byte> writer)
    {
        lock (replacementMutex_)
        {
            if (activeReplacement_)
            {
                writer.Reset();
            }
            else
            {
                var serialized = writer.ExtractAndReplace();
                predictQueue_.Enqueue(serialized);
            }
        }
    }
}
