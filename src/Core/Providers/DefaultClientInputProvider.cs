namespace Core.Providers;

/// <summary>
/// The default implementation of the <see cref="IClientInputProvider{TClientInput}"/>.
/// Uses the parameterless constructor to create the default input instance.
/// </summary>
/// <typeparam name="TClientInput">The type of the client input.</typeparam>
public class DefaultClientInputProvider<TClientInput> : IClientInputProvider<TClientInput> where TClientInput : class, new()
{
    /// <inheritdoc/>
    public TClientInput GetInput() => new();
}
