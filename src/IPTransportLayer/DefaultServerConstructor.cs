using Core;
using Core.Providers;
using Core.Server;
using Core.Transport;
using DefaultTransport.Client;
using DefaultTransport.Server;

namespace DefaultTransport;

public static class DefaultServerConstructor
{
    public static Server<TC, TS, TG> Construct<TC, TS, TG>(IServerTransport transport, IServerInputProvider<TS, TG>? inputProvider = null,IDisplayer<TG>? displayer = null)
        where TC : class, new()
        where TS : class, new()
        where TG : class, IGameState<TC, TS>, new()
    {
        Server<TC, TS, TG> server = new(new DefaultServerDispatcher(transport), displayer, inputProvider);

        IServerSession session = server;

        // TODO: do
        // transport.OnMessage += (id, message) => message.Inform(session, id);
        transport.OnClientJoin += session.AddClient;
        transport.OnClientFinish += session.FinishClient;

        return server;
    }
}
