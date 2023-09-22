using System.Net;
using Core;
using Core.Server;
using Core.Transport;
using DefaultTransport.Client;
using DefaultTransport.Server;
using DefaultTransport.TcpTransport;

namespace DefaultTransport;

public static class DefaultServerConstructor
{
    public static Server<TClientInput, TServerInput, TGameState, TUpdateInfo> Construct<TClientInput, TServerInput, TGameState, TUpdateInfo>(IServerTransport<IMessageToServer, IMessageToClient> transport)
        where TClientInput : class, new()
        where TServerInput : class, new()
        where TGameState : class, IGameState<TClientInput, TServerInput, TUpdateInfo>, new()
        where TUpdateInfo : new()
    {
        Server<TClientInput, TServerInput, TGameState, TUpdateInfo> server = new()
        {
            Dispatcher = new DefaultServerDispatcher(transport)
        };

        IServerSession sessionCapture = server.Session;

        transport.OnMessage += (id, message) => message.Inform(sessionCapture, id);
        transport.OnClientJoin += server.Session.AddClient;
        transport.OnClientFinish += server.Session.FinishClient;

        return server;
    }
}
