using System;

namespace Kcky.GameNewt.Client;

/// <summary>
/// Provides input predictions for a given game.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
/// <typeparam name="TServerInput">The type of the server input.</typeparam>
/// <typeparam name="TGameState">The type of the game state.</typeparam>
sealed class UpdateInputPredictor<TClientInput, TServerInput, TGameState> where TGameState : class, IGameState<TClientInput, TServerInput>, new()
    where TClientInput : class, new()
    where TServerInput : class, new()
{
    readonly PredictClientInputDelegate<TClientInput> predictClientInput_;
    readonly PredictServerInputDelegate<TServerInput, TGameState> predictServerInput_;
    readonly int localId_;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="predictServerInput">Delegate which predicts server input.</param>
    /// <param name="predictClientInput">Delegate which predicts client input.</param>
    /// <param name="localId">The ID of the local clients.</param>
    public UpdateInputPredictor(PredictClientInputDelegate<TClientInput> predictClientInput,
        PredictServerInputDelegate<TServerInput, TGameState> predictServerInput,
        int localId)
    {
        predictClientInput_ = predictClientInput;
        predictServerInput_ = predictServerInput;
        localId_ = localId;
    }

    /// <summary>
    /// Performs input predictions on the inputs structure,
    /// </summary>
    /// <param name="input"></param>
    /// <param name="localInput"></param>
    /// <param name="state"></param>
    public void Predict(ref UpdateInput<TClientInput, TServerInput> input, TClientInput localInput, TGameState state)
    {
        PredictClientInputs(input.ClientInputInfos.Span, localInput);
        predictServerInput_(ref input.ServerInput, state);
    }

    void PredictClientInputs(Span<UpdateClientInfo<TClientInput>> inputs, TClientInput localInput)
    {
        // Terminated players are going to be predicted as well, but the game state update should ignore the removed player.

        int length = inputs.Length;
        for (int i = 0; i < length; i++)
        {
            ref var info = ref inputs[i];

            if (info.Id == localId_)
            {
                info.Input = localInput;
            }
            else
            {
                predictClientInput_(ref info.Input);
            }
        }
    }
}
