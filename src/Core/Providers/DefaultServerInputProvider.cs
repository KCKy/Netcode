using Core.Utility;

namespace Core.Providers;

public class DefaultServerInputProvider<TServerInput, TGameState> : IServerInputProvider<TServerInput, TGameState>
    where TServerInput : class, new()
    where TGameState : class, new()
{
    public TServerInput GetInput(TGameState info) => DefaultProvider<TServerInput>.Create();
}
