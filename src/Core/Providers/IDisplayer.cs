namespace Core.Providers;

public interface IDisplayer<TGameState>
{
    public void AddAuthoritative(long frame, TGameState gameState);
    public void AddPredict(long frame, TGameState gameState);
}
