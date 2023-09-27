namespace Core.Providers;

public interface IClientInputProvider<TClientInput> where TClientInput : class, new()
{
    public TClientInput GetInput();
}
