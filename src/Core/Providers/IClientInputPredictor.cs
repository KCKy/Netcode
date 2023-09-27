namespace Core.Providers;

public interface IClientInputPredictor<TClientInput>
    where TClientInput : class, new()
{
    void PredictInput(ref TClientInput previous);
}
