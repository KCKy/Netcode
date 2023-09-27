using Core.Utility;

namespace Core.Providers;

public class DefaultServerInputPredictor<TServerInput, TGameState> : IServerInputPredictor<TServerInput, TGameState>
    where TServerInput : class, new()
    where TGameState : class, new()
{
    public void PredictInput(ref TServerInput input, TGameState state) { }
}
