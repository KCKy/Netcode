using System.Diagnostics;
using Kcky.GameNewt.Utility;
using Microsoft.Extensions.Logging;

namespace Kcky.GameNewt.Client;

/// <summary>
/// Used to give finished replacement states to the prediction simulation atomically.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
sealed class ReplacementReceiver<TClientInput, TServerInput, TGameState> where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new()
{
    readonly StateHolder<TClientInput, TServerInput, TGameState, MiscStateType> receiverHolder_;

    UpdateInput<TClientInput, TServerInput> newPredictInput_ = UpdateInput<TClientInput, TServerInput>.Empty;
    bool isReplaced_ = false;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use for logging.</param>
    /// <param name="frame">The frame number the prediction simulation is at.</param>
    public ReplacementReceiver(ILoggerFactory loggerFactory, long frame)
    {
        receiverHolder_ = new(loggerFactory)
        {
            Frame = frame
        };
    }

    /// <summary>
    /// Tries to finish replacement by giving the replacement state to be used for the prediction simulation.
    /// </summary>
    /// <param name="holder">The holder to take the state from. A different state shall be put it if the original state is used.</param>
    /// <param name="input">The latest authoritative update input to use for next predictions.</param>
    /// <returns>Zero if the replacement state has been received or a difference between frame numbers if the replacement is not finished.</returns>
    /// <remarks>
    /// If the return value is not zero, the caller is expected to update the replacement state such that the frame numbers would match.
    /// </remarks>
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

    /// <summary>
    /// Tries to get a received a replacement if one available.
    /// Also signals which frame the next replacement shall have.
    /// </summary>
    /// <param name="frame">The frame the next replacements shall finish at.</param>
    /// <param name="holder">The holder to put the new replacement state in.</param>
    /// <param name="input">The latest authoritative input to use for input predictions.</param>
    /// <returns>Whether a new replacement state has been put into the prediction state holder.</returns>
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
