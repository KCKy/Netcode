using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Kcky.Useful;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kcky.GameNewt.Client;

sealed class ReplacementCoordinator(ILoggerFactory loggerFactory)
{
    readonly object replacementMutex_ = new();
    bool activeReplacement_ = false;
    long currentReplacement_ = long.MinValue;

    readonly ConcurrentQueue<Memory<byte>> predictQueue_ = new();
    readonly ILogger logger_ = loggerFactory.CreateLogger<ReplacementCoordinator>();

    public bool TryDequeuePredictInput(out Memory<byte> input) => predictQueue_.TryDequeue(out input);

    public void Init()
    {
        predictQueue_.Clear();
    }

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


    public void FinishReplacement(long index)
    {
        lock (replacementMutex_)
        {
            Debug.Assert(currentReplacement_ >= index);
            if (currentReplacement_ == index)
                activeReplacement_ = false;
            logger_.Debug("Replacement finished (A:{Active})", activeReplacement_);
        }
    }

    public bool CheckReplacementCurrent(long index)
    {
        lock (replacementMutex_)
            return currentReplacement_ <= index;
    }

    public void Stop()
    {
        lock (replacementMutex_)
            currentReplacement_ = long.MaxValue;
    }

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
