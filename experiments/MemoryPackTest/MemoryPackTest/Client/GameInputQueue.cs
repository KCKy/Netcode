using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FrameworkTest;

public sealed class GameInputQueue<TInput> : IAsyncEnumerable<TInput>
{
    long currentFrame_ = -1;
    readonly PriorityQueue<TInput, long> inputQueue_ = new();
    readonly HashSet<long> heldFrames_ = new();
    readonly object mutex_ = new();
    TaskCompletionSource? gotCurrentFrame_ = null;
    bool createdEnumerator_ = false;

    public long CurrentFrame
    {
        get
        {
            lock (mutex_)
                return currentFrame_;
        }
        set
        {
            lock (mutex_)
            {
                currentFrame_ = value;

                long frame;

                while (inputQueue_.TryPeek(out _, out frame) && frame < value)
                {
                    inputQueue_.Dequeue();
                    heldFrames_.Remove(frame);
                }

                if (frame == value)
                    gotCurrentFrame_?.TrySetResult();
            }
        }
    }

    public void AddInputFrame(TInput input, long frame)
    {
        lock (mutex_)
        {
            if (frame < currentFrame_)
                return;

            if (heldFrames_.Add(frame))
                inputQueue_.Enqueue(input, frame);

            if (frame == currentFrame_)
                gotCurrentFrame_?.TrySetResult();
        }
    }
    
    class Enumerator : IAsyncEnumerator<TInput>
    {
        readonly GameInputQueue<TInput> queue_;

        public Enumerator(GameInputQueue<TInput> queue)
        {
            queue_ = queue;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async ValueTask<bool> MoveNextAsync()
        {
            lock (queue_.mutex_)
            {
                if (queue_.inputQueue_.TryPeek(out var input, out long frame))
                {
                    if (frame == queue_.currentFrame_)
                    {
                        Current = queue_.inputQueue_.Dequeue();
                        queue_.heldFrames_.Remove(frame);
                        Console.WriteLine(frame);
                        queue_.currentFrame_++;
                        return true;
                    }
                }

                queue_.gotCurrentFrame_ = new TaskCompletionSource();
            }

            await queue_.gotCurrentFrame_.Task;

            lock (queue_.mutex_)
            {
                queue_.heldFrames_.Remove(queue_.currentFrame_);
                Current = queue_.inputQueue_.Dequeue();
                Console.WriteLine(queue_.currentFrame_);
                queue_.currentFrame_++;
            }

            return true;
        }

        public TInput Current { get; private set; } = default!;
    }
    
    public IAsyncEnumerator<TInput> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        if (createdEnumerator_)
            throw new InvalidOperationException("Only one enumerator is allowed.");

        createdEnumerator_ = true;

        return new Enumerator(this);
    }
}
