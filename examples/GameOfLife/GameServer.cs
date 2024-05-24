using System;
using System.Net;
using System.Threading.Tasks;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport.Default;
using Microsoft.Extensions.Logging;

namespace GameOfLife;

class GameServer(int port, ILoggerFactory loggerFactory)
{
    static ServerInput ProvideServerInput(GameState info)
    {
        ServerInput ret = new();

        if (info.Frame % 50 == 0)
            ret.CellRespawnEventSeed = new Random().Next() + 1;

        return ret;
    }

    public Task RunAsync()
    {
        IPEndPoint endPoint = new(IPAddress.Any, port);
        IpServerTransport transport = new(endPoint);
        DefaultServerDispatcher dispatcher = new(transport);

        Server<ClientInput, ServerInput, GameState> server = new(dispatcher, loggerFactory)
        {
            ServerInputProvider = ProvideServerInput
        };

        return server.RunAsync();
    }
}
