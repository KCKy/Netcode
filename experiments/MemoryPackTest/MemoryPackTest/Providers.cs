namespace FrameworkTest;

public interface IServerInputProvider<TServerInput>
{
    public TServerInput GetInput();
    public TServerInput GetInitialInput();
}

public interface IServerDisplayer<TPlayerInput, TServerInput, TGameState> 
    where TGameState : IGameState<TPlayerInput, TServerInput>
{
    void AddFrame(TGameState state, long frame);
}

public interface IClientDisplayer<TPlayerInput, TServerInput, TGameState>
    where TGameState : IGameState<TPlayerInput, TServerInput>
{
    void AddFrame(TGameState state, long frame);
    void SetId(long id);
}

public interface IPlayerInputProvider<TClientInput>
{
    TClientInput GetInput(long frame);
}

public interface IInputPredictor<TClientInput, TServerInput, TGameState>
{
    // TODO: give more context?

    public Input<TClientInput, TServerInput> PredictInput(TGameState state);
}
