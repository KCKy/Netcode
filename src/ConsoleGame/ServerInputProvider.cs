using Core.Providers;

namespace TestGame;

sealed class ServerInputProvider : IServerInputProvider<ServerInput, GameState>
{
    readonly Random random_ = new();

    public ServerInput GetInput(GameState info)
    {
        ServerInput input = new();
        return input;

        if (random_.NextDouble() >= 0.05)
            return input;

        int x = random_.Next(0, GameState.LevelWidth);
        int y = random_.Next(0, GameState.LevelHeight);

        input.FoodSpawnEvent = new FoodSpawnEvent()
        {
            Type = FoodType.Carrot,
            X = x,
            Y = y
        };

        return input;
    }
}
