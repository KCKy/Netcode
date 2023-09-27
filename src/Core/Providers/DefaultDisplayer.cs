namespace Core.Providers;

public class DefaultDisplayer<TGameState> : IDisplayer<TGameState>
{
    public void AddAuthoritative(long frame, TGameState gameState) { }
    public void AddPredict(long frame, TGameState gameState) { }
}
