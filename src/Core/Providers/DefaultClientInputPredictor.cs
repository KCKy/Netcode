namespace Kcky.GameNewt.Providers;

/// <summary>
/// Default implementation of <see cref="IClientInputPredictor{TClientInput}"/>.
/// Predicts that the remains unchanged (i.e. does nothing).
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
public class DefaultClientInputPredictor<TClientInput> : IClientInputPredictor<TClientInput>
    where TClientInput : class, new()
{
    /// <inheritdoc/>
    public void PredictInput(ref TClientInput previous) { }
}
