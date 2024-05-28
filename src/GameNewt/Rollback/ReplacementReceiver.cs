using System.Diagnostics;
using Kcky.GameNewt.Utility;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

sealed class ReplacementReceiver<TClientInput, TServerInput, TGameState> where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new()
{
    readonly StateHolder<TClientInput, TServerInput, TGameState, MiscStateType> receiverHolder_;

    UpdateInput<TClientInput, TServerInput> newPredictInput_ = UpdateInput<TClientInput, TServerInput>.Empty;
    bool isReplaced_ = false;

    public ReplacementReceiver(ILoggerFactory loggerFactory, long frame)
    {
        receiverHolder_ = new(loggerFactory)
        {
            Frame = frame
        };
    }

    public long TryGive(StateHolder<TClientInput, TServerInput, TGameState, ReplacementStateType> holder, in UpdateInput<TClientInput, TServerInput> input)
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

    public bool TryReceive(long frame, StateHolder<TClientInput, TServerInput, TGameState, PredictiveStateType> holder, ref UpdateInput<TClientInput, TServerInput> input)
    {
        lock (receiverHolder_)
        {
            bool success = isReplaced_;
            if (success)
            {
                Debug.Assert(receiverHolder_.Frame == holder.Frame);
                (receiverHolder_.State, holder.State) = (holder.State, receiverHolder_.State);
                input = newPredictInput_;
                isReplaced_ = false;
            }

            receiverHolder_.Frame = frame;

            return success;
        }
    }
}
