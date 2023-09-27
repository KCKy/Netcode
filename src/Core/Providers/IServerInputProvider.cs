namespace Core.Providers;

public interface IServerInputProvider<TServerInput, TGameState>
    where TServerInput : class, new()
    where TGameState : class, new()
{
    TServerInput GetInput(TGameState info);
}
