using Core;
using Core.Providers;
using Core.Client;
using Core.Transport;
using DefaultTransport.Client;
using DefaultTransport.Server;

namespace DefaultTransport;

public static class DefaultClientConstructor
{
    public static Client<TC, TS, TG> Construct<TC, TS, TG>(IClientTransport<IMessageToClient, IMessageToServer> transport,
        IClientInputProvider<TC>? inputProvider,
        IServerInputPredictor<TS, TG>? serverInputPredictor = null,
        IClientInputPredictor<TC>? clientInputPredictor = null,
        IDisplayer<TG>? displayer = null)
        where TC : class, new()
        where TS : class, new()
        where TG : class, IGameState<TC, TS>, new()
    {
        Client<TC, TS, TG> client = new(new DefaultClientDispatcher(transport), displayer, inputProvider, serverInputPredictor, clientInputPredictor);

        IClientSession session = client;

        transport.OnMessage += message => message.Inform(session);
        transport.OnFinish += session.Finish;

        // TODO: how to deregister

        return client;
    }
}
