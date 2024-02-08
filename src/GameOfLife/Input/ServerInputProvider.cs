using Core.Providers;

namespace GameOfLife
{
    class ServerInputProvider : IServerInputProvider<ServerInput, GameState>
    {
        public ServerInput GetInput(GameState info)
        {
            ServerInput ret = new();

            if (info.Frame % 50 == 0)
                ret.CellRespawnEventSeed = new Random().Next() + 1;

            return ret;
        }
    }
}
