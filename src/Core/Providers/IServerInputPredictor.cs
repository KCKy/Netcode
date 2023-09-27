namespace Core.Providers;

public interface IServerInputPredictor<TServerInput, TGameState>
    where TServerInput : class, new()
    where TGameState : new()
{
    void PredictInput(ref TServerInput input, TGameState info);
}
