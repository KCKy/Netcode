using System.Net;
using System.Threading.Tasks;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport.Default;
using Microsoft.Extensions.Logging;

namespace TopDownShooter;

class GameServer(int port, ILoggerFactory loggerFactory)
{
    public Task RunAsync()
    {
        IPEndPoint endPoint = new(IPAddress.Any, port);
        IpServerTransport transport = new(endPoint);
        DefaultServerDispatcher dispatcher = new(transport);

        Server<ClientInput, ServerInput, GameState> server = new(dispatcher, loggerFactory)
        {
            ClientInputPredictor = ClientInputPrediction.PredictClientInput
        };

        return server.RunAsync();
    }
}
