using Kcky.GameNewt.Client;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Transport.Default;
using System.Net;

namespace Advanced;

class GameClient
{
    readonly Client<ClientInput, ServerInput, GameState> client_;
    int localId_;

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
        client_.OnNewPredictiveState += Draw;
    }

    static void PredictInput(ref ClientInput input)
    {
        input.PlaceFlag = false;
    }

    void Init(int id) => localId_ = id;

    public void Run()
    {
        Console.Clear();
        Console.SetCursorPosition(0, 0);

        Task task = client_.RunAsync();

        while (!task.IsCompleted)
            client_.Update();

        task.Wait();
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

    void Draw(long frame, GameState state)
    {
        if (state.EndScreen is { } screen)
        {
            Console.Clear();
            DrawEndScreen(screen);
            return;
        }

        DateTime datetime = DateTimeOffset.FromUnixTimeSeconds(state.LatestPlayerConnectionTime).LocalDateTime;

        Console.WriteLine($"My ID: {localId_} Frame: {frame} Players: {state.IdToPlayer.Count} Latest connected: {datetime}");

        var idToPlayer = state.IdToPlayer;
        int[,] flags = state.PlacedFlags;

        for (int y = 0; y < GameState.MapSize; y++)
        {
            for (int x = 0; x < GameState.MapSize; x++)
            {
                int value = flags[x, y];
                switch (value)
                {
                    case 0:
                        Console.Write(state.IsTrapped[x, y] ? '!' : '.');
                        break;
                    case > 0:
                        Console.Write(value % 10);
                        break;
                }
            }
            Console.WriteLine();
        }

        foreach ((int playerId, PlayerInfo info) in idToPlayer)
        {
            Console.SetCursorPosition(info.X, info.Y + 1);
            if (playerId == localId_)
            {
                Console.Write('#');
            }
            else
            {
                char icon = (char)('A' + (char)((playerId - 1) % 26));
                Console.Write(icon);
            }
        }

        Console.SetCursorPosition(0, 0);
    }
}
