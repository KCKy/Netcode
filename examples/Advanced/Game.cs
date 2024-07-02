using MemoryPack;
using Kcky.GameNewt;
using Microsoft.Extensions.Logging;

namespace Advanced;

enum Direction
{
    None = 0,
    Up,
    Down,
    Left,
    Right
}

[MemoryPackable(GenerateType.CircularReference, SerializeLayout.Sequential)]
partial class ClientInput
{
    public Direction Direction;
    public bool PlaceFlag;
}

[MemoryPackable(GenerateType.CircularReference, SerializeLayout.Sequential)]
partial class ServerInput
{
    public long SetLatestConnectionTime;
}

[MemoryPackable(GenerateType.CircularReference, SerializeLayout.Sequential)]
partial class EndScreen
{
    public int RemainingTicks = GameState.TickRateWhole * 5;
    public int TrapCount;
    public SortedDictionary<int, int> PlayerToFlags = new();

    public static EndScreen Create(GameState state)
    {
        EndScreen screen = new();

        int[,] flags = state.PlacedFlags;
        bool[,] traps = state.IsTrapped;

        for (int x = 0; x < GameState.MapSize; x++)
        for (int y = 0; y < GameState.MapSize; y++)
        {
            if (traps[x, y])
                screen.TrapCount++;

            screen.TrapCount += traps[x, y] ? 1 : 0;

            int key = flags[x, y];
            if (key <= 0)
                continue;

            screen.PlayerToFlags.TryAdd(key, 0);
            screen.PlayerToFlags[key] += 1;
        }

        return screen;
    }

    public UpdateOutput Update()
    {
        if (RemainingTicks <= 0)
        {
            return new UpdateOutput()
            {
                ShallStop = true
            };
        }

        RemainingTicks--;

        return UpdateOutput.Empty;
    }
}

[MemoryPackable(GenerateType.CircularReference, SerializeLayout.Sequential)]
partial class PlayerInfo
{
    public int X;
    public int Y;
}

[MemoryPackable(GenerateType.CircularReference, SerializeLayout.Sequential)]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    public SortedDictionary<int, PlayerInfo> IdToPlayer;
    public int[,] PlacedFlags;
    public bool[,] HasTrap;
    public long LatestPlayerConnectionTime;
    public EndScreen? EndScreen = null;
    public const int MapSize = 10;

    public GameState()
    {
        IdToPlayer = new();
        PlacedFlags = new int[MapSize, MapSize];
        HasTrap = new bool[MapSize, MapSize];
    }

    public const int TickRateWhole = 5;
    public static float DesiredTickRate => TickRateWhole;
    
    int CountFilledTiles()
    {
        int tiles = 0;
        for (int x = 0; x < MapSize; x++)
        for (int y = 0; y < MapSize; y++)
        {
            if (HasTrap[x, y] || PlacedFlags[x, y] != 0)
                tiles++;
        }

        return tiles;
    }

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        if (EndScreen is not null)
            return EndScreen.Update();

        ServerInput serverInput = updateInputs.ServerInput;
        if (serverInput.SetLatestConnectionTime >= 0)
            LatestPlayerConnectionTime = serverInput.SetLatestConnectionTime;

        List<int> toBeKickedClients = new();

        foreach (var inputInfo in updateInputs.ClientInputInfos.Span)
        {
            int id = inputInfo.Id;
            
            if (inputInfo.Terminated)
            {
                IdToPlayer.Remove(id);
                continue;
            }

            if (!IdToPlayer.ContainsKey(id))
            {
                IdToPlayer.Add(id, new PlayerInfo());
            }

            PlayerInfo playerInfo = IdToPlayer[id];

            switch (inputInfo.Input.Direction)
            {
                case Direction.Up:
                    TryMovePlayer(playerInfo, playerInfo.X, playerInfo.Y - 1);
                    break;
                case Direction.Down:
                    TryMovePlayer(playerInfo, playerInfo.X, playerInfo.Y + 1);
                    break;
                case Direction.Left:
                    TryMovePlayer(playerInfo, playerInfo.X - 1, playerInfo.Y);
                    break;
                case Direction.Right:
                    TryMovePlayer(playerInfo, playerInfo.X + 1, playerInfo.Y);
                    break;
            }

            if (inputInfo.Input.PlaceFlag)
            {
                if (HasTrap[playerInfo.X, playerInfo.Y])
                {
                    toBeKickedClients.Add(id);
                }
                else if (PlacedFlags[playerInfo.X, playerInfo.Y] == 0)
                {
                    PlacedFlags[playerInfo.X, playerInfo.Y] = id;
                }
            }
        }
        
        if (CountFilledTiles() == MapSize * MapSize)
            EndScreen = EndScreen.Create(this);

        UpdateOutput output = new()
        {
            ClientsToTerminate = toBeKickedClients.ToArray()
        };

        return output;
    }

    bool TryMovePlayer(PlayerInfo info, int newX, int newY)
    {
        if (newX < 0 || newX >= MapSize || newY < 0 || newY >= MapSize)
            return false;

        info.X = newX;
        info.Y = newY;

        return true;
    }
}
