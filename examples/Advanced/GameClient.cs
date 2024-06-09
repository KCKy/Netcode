using Kcky.GameNewt.Client;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Transport.Default;
using System.Net;
using Kcky.GameNewt.Utility;
using Kcky.Useful;

namespace Advanced;

class GameClient
{
    readonly Client<ClientInput, ServerInput, GameState> client_;
    int localId_;
    volatile bool updateDraw_ = false;
    
    public GameClient()
    {
        IPEndPoint serverAddress = new(IPAddress.Loopback, 42000);
        IpClientTransport transport = new(serverAddress);
        DefaultClientDispatcher dispatcher = new(transport);

        client_ = new(dispatcher)
        {
            ClientInputProvider = GetInput,
            ClientInputPredictor = PredictInput,
        };

        client_.OnInitialize += Init;
        client_.OnNewPredictiveState += HandleNewPredictiveState;
        client_.OnNewAuthoritativeState += HandleNewAuthoritativeState;
    }

    readonly PooledBufferWriter<byte> authoritativeStateBufferWriter_ = new();
    readonly object newAuthoritativeStateLock_ = new();
    bool receivedNewAuthoritativeState_= false;
    GameState newAuthoritativeState_ = new();
    GameState authoritativeState_ = new();
    GameState predictiveState_ = new();

    void HandleNewAuthoritativeState(long frame, GameState state)
    {
        lock (newAuthoritativeStateLock_)
        {
            authoritativeStateBufferWriter_.Copy(state, ref newAuthoritativeState_!);
            receivedNewAuthoritativeState_ = true;
        }

        updateDraw_ = true;
    }

    void HandleNewPredictiveState(long frame, GameState state) => updateDraw_ = true;

    void Init(int id) => localId_ = id;

    static void PredictInput(ref ClientInput input)
    {
        input.PlaceFlag = false;
    }

    public void Run()
    {
        Console.Clear();
        Console.SetCursorPosition(0, 0);

        Task task = client_.RunAsync();

        while (!task.IsCompleted)
        {
            if (client_.Update() is { } validState)
                predictiveState_ = validState;
            Draw();
        }
    }

    static ClientInput GetInput()
    {
        ClientInput input = new();

        while (Console.KeyAvailable)
        {
            ConsoleKeyInfo read = Console.ReadKey(true);
            switch (read.Key)
            {
                case ConsoleKey.A:
                    input.Direction = Direction.Left;
                    break;
                case ConsoleKey.W:
                    input.Direction = Direction.Up;
                    break;
                case ConsoleKey.S:
                    input.Direction = Direction.Down;
                    break;
                case ConsoleKey.D:
                    input.Direction = Direction.Right;
                    break;
                case ConsoleKey.Spacebar:
                    input.PlaceFlag = true;
                    break;
            }
        }

        return input;
    }

    static void DrawEndScreen(EndScreen screen)
    {
        Console.WriteLine("Game over.");
        Console.WriteLine($"Number of traps: {screen.TrapCount}");
        foreach ((int id, int count) in screen.PlayerToFlags)
            Console.WriteLine($"Player {id} placed {count} flags.");

        Console.WriteLine();
        Console.WriteLine($"The game will end in {screen.RemainingTicks / GameState.TickRateWhole} seconds.");
    }

    void Draw()
    {
        if (Monitor.TryEnter(newAuthoritativeStateLock_))
        {
            if (receivedNewAuthoritativeState_)
            {
                receivedNewAuthoritativeState_ = false;
                (authoritativeState_, newAuthoritativeState_) = (newAuthoritativeState_, authoritativeState_);
            }

            Monitor.Exit(newAuthoritativeStateLock_);
        }

        if (!updateDraw_)
            return;
        
        updateDraw_ = false;

        if (predictiveState_.EndScreen is { } screen)
        {
            Console.Clear();
            DrawEndScreen(screen);
            return;
        }

        DateTime datetime = DateTimeOffset.FromUnixTimeSeconds(predictiveState_.LatestPlayerConnectionTime).LocalDateTime;

        Console.WriteLine($"My ID: {localId_} Frame: {client_.PredictFrame} Players: {predictiveState_.IdToPlayer.Count} Latest connected: {datetime}");

        for (int y = 0; y < GameState.MapSize; y++)
        {
            for (int x = 0; x < GameState.MapSize; x++)
            {
                int tile = predictiveState_.PlacedFlags[x, y];
                Console.Write(predictiveState_.IsTrapped[x, y] ? '!' : GetTileChar(tile));
            }
            Console.WriteLine();
        }

        foreach ((int playerId, PlayerInfo info) in authoritativeState_.IdToPlayer)
        {
            if (playerId == localId_)
                continue;
            Console.SetCursorPosition(info.X, info.Y + 1);
            Console.Write('O');
        }

        if (predictiveState_.IdToPlayer.TryGetValue(localId_, out PlayerInfo? localInfo))
        {
            Console.SetCursorPosition(localInfo.X, localInfo.Y + 1);
            Console.Write('#');
        }

        Console.SetCursorPosition(0, 0);
    }

    char GetTileChar(int tile)
    {
        if (tile == 0)
            return '.';
        return tile == localId_ ? 'X' : '@';
    }
}
