using System.Net;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport.Default;

namespace Basic;

class GameServer
{
    public Task RunAsync()
    {
        IPEndPoint serverAddress = new(IPAddress.Any, 42000);
        IpServerTransport transport = new(serverAddress);
        DefaultServerDispatcher dispatcher = new(transport);
        
        Server<ClientInput, ServerInput, GameState> server = new(dispatcher);

        return server.RunAsync();
    }
}
