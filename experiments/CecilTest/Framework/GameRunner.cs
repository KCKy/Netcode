using MessagePack;

namespace Framework;

public class GameRunner<TGameState> where TGameState : IGameState
{
    public delegate void StateUpdateDelegate(TGameState state);

    readonly StateUpdateDelegate stateUpdate_;
    readonly TGameState initialState_;

    public GameRunner(StateUpdateDelegate stateUpdate, TGameState initialState)
    {
        stateUpdate_ = stateUpdate;
        initialState_ = initialState;
    }

    public void Run()
    {

        var serialized = MessagePackSerializer.Serialize(initialState_, MessagePack.Resolvers.ContractlessStandardResolver.Options);
        var copy = MessagePackSerializer.Deserialize<TGameState>(serialized, MessagePack.Resolvers.ContractlessStandardResolver.Options);

        Console.WriteLine(serialized);
        Console.WriteLine(copy);

        stateUpdate_(copy);

        Console.WriteLine(copy);
    }
}
