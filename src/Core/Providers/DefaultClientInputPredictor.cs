namespace Core.Providers;

public class DefaultClientInputPredictor<TClientInput> : IClientInputPredictor<TClientInput>
    where TClientInput : class, new()
{
    public void PredictInput(ref TClientInput previous) { }
}
