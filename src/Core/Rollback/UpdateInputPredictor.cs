using System;
using Kcky.GameNewt.Providers;

namespace Kcky.GameNewt.Client;

sealed class UpdateInputPredictor<TC, TS, TG>
    (IClientInputPredictor<TC> clientInputPredictor,
        IServerInputPredictor<TS, TG> serverInputPredictor)
    where TG : class, IGameState<TC, TS>, new()
    where TC : class, new()
    where TS : class, new()
{
    public int LocalId { private get; set; }

    public void Predict(ref UpdateInput<TC, TS> input, TC localInput, TG state)
    {
        // Predict other input
        PredictClientInput(input.ClientInputInfos.Span, localInput);
        serverInputPredictor.PredictInput(ref input.ServerInput, state);
    }

    void PredictClientInput(Span<UpdateClientInfo<TC>> inputs, TC localInput)
    {
        // ServerTerminated players are going to be predicted as well, but the game state update should ignore the removed player.

        int localId = LocalId;

        int length = inputs.Length;
        for (int i = 0; i < length; i++)
        {
            ref var info = ref inputs[i];

            if (info.Id == localId)
            {
                info.Input = localInput;
            }
            else
            {
                clientInputPredictor.PredictInput(ref info.Input);
            }
        }
    }
}
