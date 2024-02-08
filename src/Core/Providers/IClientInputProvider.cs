namespace Core.Providers;

/// <summary>
/// Client-side component responsible for providing specific client's input into the state update.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
public interface IClientInputProvider<TClientInput> where TClientInput : class, new()
{
    /// <summary>
    /// Get input from the specific client.
    /// </summary>
    /// <returns>Current input of the client.</returns>
    /// <remarks>
    /// This is expected to gather recent keypresses, UI events, mouse movement etc.
    /// </remarks>
    public TClientInput GetInput();
}
