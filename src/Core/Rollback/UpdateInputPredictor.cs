using System;

namespace Kcky.GameNewt.Client;

sealed class UpdateInputPredictor<TC, TS, TG> where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    readonly PredictClientInputDelegate<TC> predictClientInput_;
    readonly PredictServerInputDelegate<TS, TG> predictServerInput_;
    readonly int localId_;

    public UpdateInputPredictor(PredictClientInputDelegate<TC> predictClientInput,
        PredictServerInputDelegate<TS, TG> predictServerInput,
        int localId)
    {
        predictClientInput_ = predictClientInput;
        predictServerInput_ = predictServerInput;
        localId_ = localId;
    }

    public void Predict(ref UpdateInput<TC, TS> input, TC localInput, TG state)
    {
        PredictClientInputs(input.ClientInputInfos.Span, localInput);
        predictServerInput_(ref input.ServerInput, state);
    }

    void PredictClientInputs(Span<UpdateClientInfo<TC>> inputs, TC localInput)
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
