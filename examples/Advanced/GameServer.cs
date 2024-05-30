using System.Net;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport.Default;

namespace Advanced;

class GameServer
{
    readonly Server<ClientInput, ServerInput, GameState> server_;

    public GameServer()
    {
        IPEndPoint serverAddress = new(IPAddress.Any, 42000);
        IpServerTransport transport = new(serverAddress);
        transport.OnClientJoin += HandleClientJoin;

        DefaultServerDispatcher dispatcher = new(transport);
        server_ = new(dispatcher)
        {
            ServerInputProvider = GetInput
        };

        server_.OnStateInit += PlaceTraps;
    }

    readonly object newConnectionTimeLock_ = new();
    long newConnectionTime_ = long.MinValue;

    void HandleClientJoin(int _)
    {
        long connectionTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        lock (newConnectionTimeLock_)
            newConnectionTime_ = connectionTime;
    }

    ServerInput GetInput(GameState state)
    {
        ServerInput input = new();

        lock (newConnectionTimeLock_)
        {
            input.SetLatestConnectionTime = newConnectionTime_;
            newConnectionTime_ = long.MinValue;
        }

        return input;
    }

    static void PlaceTraps(GameState state)
    {
        Random random = new();

        for (int x = 0; x < GameState.MapSize; x++)
        for (int y = 0; y < GameState.MapSize; y++)
            state.IsTrapped[x, y] = random.NextSingle() < 0.33f;
    }

    public async Task RunAsync()
    {
        try
        {
            await server_.RunAsync();
        }
        catch (OperationCanceledException) { }
    }
}
