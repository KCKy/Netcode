using Kcky.GameNewt.Client;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Transport.Default;
using System.Net;

namespace Basic;

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
            ClientInputProvider = GetInput
        };
        client_.OnInitialize += Init;
        client_.OnNewPredictiveState += Draw;
    }

    void Init(int id) => localId_ = id;

    public void Run()
    {
        Console.Clear();
        Console.SetCursorPosition(0, 0);
        Console.CursorVisible = false;

        Task task = client_.RunAsync();

        while (!task.IsCompleted)
            client_.Update();
    }
    
    void Draw(long frame, GameState state)
    {
        Console.WriteLine($"My ID: {localId_} Frame: {frame} Players: {state.IdToPlayer.Count}");

        for (int y = 0; y < GameState.MapSize; y++)
        {
            for (int x = 0; x < GameState.MapSize; x++)
            {
                int tile = state.PlacedFlags[x, y];
                Console.Write(GetTileChar(tile));
            }
            Console.WriteLine();
        }

        foreach ((int playerId, PlayerInfo info) in state.IdToPlayer)
        {
            Console.SetCursorPosition(info.X, info.Y + 1);
            Console.Write(GetPlayerChar(playerId));
        }

        Console.SetCursorPosition(0, 0);
    }

    char GetTileChar(int tile)
    {
        if (tile == 0)
            return '.';
        return tile == localId_ ? 'X' : '@';
    }

    char GetPlayerChar(int id) => id == localId_ ? '#' : 'O';

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
}
