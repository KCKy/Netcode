using Core.Utility;

namespace Core.Providers;

public class DefaultClientInputProvider<TClientInput> : IClientInputProvider<TClientInput> where TClientInput : class, new()
{
    public TClientInput GetInput() => DefaultProvider<TClientInput>.Create();
}
