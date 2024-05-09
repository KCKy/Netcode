using Kcky.GameNewt.Providers;

namespace Basic;

class Displayer : IDisplayer<GameState>
{
    int localId_ = -1;

    public Displayer()
    {
        Console.Clear();
        Console.SetCursorPosition(0, 0);
    }

    public void Init(int id)
    {
        localId_ = id;
    }

    public void AddAuthoritative(long frame, GameState gameState) { }

    public void AddPredict(long frame, GameState gameState)
    {
        Console.WriteLine($"My ID: {localId_} Frame: {frame} Player: {gameState.IdToPlayer.Count}");

        var idToPlayer = gameState.IdToPlayer;
        int[,] flags = gameState.PlacedFlags;

        for (int y = 0; y < GameState.MapSize; y++)
        {
            for (int x = 0; x < GameState.MapSize; x++)
            {
                int value = flags[x, y];
                switch (value)
                {
                    case 0:
                        Console.Write('.');
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
