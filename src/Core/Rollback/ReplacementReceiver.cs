using System.Diagnostics;
using Kcky.GameNewt.Utility;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

sealed class ReplacementReceiver<TC, TS, TG>(ILoggerFactory loggerFactory)
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly StateHolder<TC, TS, TG, MiscStateType> receiverHolder_ = new(loggerFactory);
    UpdateInput<TC, TS> newPredictInput_ = UpdateInput<TC, TS>.Empty;
    bool isReplaced_ = false;

    public void Init(long frame)
    {
        receiverHolder_.Frame = frame;
    }

    public long TryGive(StateHolder<TC, TS, TG, ReplacementStateType> holder, in UpdateInput<TC, TS> input)
    {
        lock (receiverHolder_)
        {
            long difference = receiverHolder_.Frame - holder.Frame;

            if (difference != 0)
                return difference;
            
            // Replacement was successful
            (holder.State, receiverHolder_.State) = (receiverHolder_.State, holder.State);

            newPredictInput_ = input;
            isReplaced_ = true;
        }

        return 0;
    }

    public void TryReceive(long frame, StateHolder<TC, TS, TG, PredictiveStateType> holder, ref UpdateInput<TC, TS> input)
    {
        lock (receiverHolder_)
        {
            if (isReplaced_)
            {
                Debug.Assert(receiverHolder_.Frame == holder.Frame);
                (receiverHolder_.State, holder.State) = (holder.State, receiverHolder_.State);
                input = newPredictInput_;
                isReplaced_ = false;
            }

            receiverHolder_.Frame = frame;
        }
    }
}
