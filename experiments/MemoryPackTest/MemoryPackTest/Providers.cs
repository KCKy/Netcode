public interface IServerInputProvider<TServerInput, TServerOutput>
{
    public TServerInput GetInput(TServerOutput output);

    public TServerInput GetInitialInput();
}

/*
public interface IPlayerInputProvider<TClientInput>
{
    TClientInput GetInput();
}

public interface IGameDisplayer<TPlayerInput, TServerInput, TServerOutput, TGameState> 
    where TGameState : IGameState<TPlayerInput, TServerInput, TServerOutput>, new()
    where TServerOutput : IServerOutput
{
    void AddNextFrame(Input<TPlayerInput, TServerInput> input, TGameState state, TServerOutput output);
}
*/
