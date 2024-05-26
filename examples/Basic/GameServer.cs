using System.Net;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport.Default;

namespace Basic;

class GameServer
{
    readonly Server<ClientInput, ServerInput, GameState> server_;

    public GameServer()
    {
        IPEndPoint serverAddress = new(IPAddress.Any, 42000);
        IpServerTransport transport = new(serverAddress);
        DefaultServerDispatcher dispatcher = new(transport);
        server_ = new(dispatcher);
    }

    public Task RunAsync() => server_.RunAsync();
}
