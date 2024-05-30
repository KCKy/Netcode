using MemoryPack;
using Kcky.GameNewt;
using Microsoft.Extensions.Logging;

namespace Advanced;

enum Direction : byte
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
partial class GameState : IGameState<ClientInput, ServerInput>
{
    [MemoryPackInclude]
    int[,] placedFlags_;

    [MemoryPackInclude]
    bool[,] isTrapped_;

    [MemoryPackInclude]
    SortedDictionary<int, PlayerInfo> idToPlayer_;

    [MemoryPackInclude]
    long latestPlayerConnectionTime_ = 0;

    [MemoryPackInclude]
    EndScreen? endScreen_ = null;

    public SortedDictionary<int, PlayerInfo> IdToPlayer => idToPlayer_;
    public int[,] PlacedFlags => placedFlags_;
    public bool[,] IsTrapped => isTrapped_;
    public long LatestPlayerConnectionTime => latestPlayerConnectionTime_;
    public EndScreen? EndScreen => endScreen_;

    public const int MapSize = 10;

    public GameState()
    {
        placedFlags_ = new int[MapSize, MapSize];
        isTrapped_ = new bool[MapSize, MapSize];
        idToPlayer_ = new();
    }

    bool TryMovePlayer(PlayerInfo info, int newX, int newY)
    {
        if (newX < 0 || newX >= placedFlags_.GetLength(0) || newY < 0 || newY >= placedFlags_.GetLength(1))
            return false;

        info.X = newX;
        info.Y = newY;

        return true;
    }

    int CountFilledTiles()
    {
        int tiles = 0;
        for (int x = 0; x < MapSize; x++)
        for (int y = 0; y < MapSize; y++)
        {
            if (isTrapped_[x, y] || placedFlags_[x, y] != 0)
                tiles++;
        }

        return tiles;
    }

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        if (endScreen_ is not null)
            return endScreen_.Update();

        ServerInput serverInput = updateInputs.ServerInput;
        if (serverInput.SetLatestConnectionTime >= 0)
            latestPlayerConnectionTime_ = serverInput.SetLatestConnectionTime;

        List<int> toBeKickedClients = new();

        foreach (var clientInputInfo in updateInputs.ClientInputInfos.Span)
        {
            int id = clientInputInfo.Id;
            ClientInput input = clientInputInfo.Input;
            bool terminated = clientInputInfo.Terminated;

            if (terminated)
            {
                idToPlayer_.Remove(id);
                continue;
            }

            if (!idToPlayer_.TryGetValue(id, out PlayerInfo? info))
            {
                info = new();
                idToPlayer_.Add(id, info);
            }

            switch (input.Direction)
            {
                case Direction.Up:
                    TryMovePlayer(info, info.X, info.Y - 1);
                    break;
                case Direction.Down:
                    TryMovePlayer(info, info.X, info.Y + 1);
                    break;
                case Direction.Left:
                    TryMovePlayer(info, info.X - 1, info.Y);
                    break;
                case Direction.Right:
                    TryMovePlayer(info, info.X + 1, info.Y);
                    break;
            }

            if (input.PlaceFlag)
            {
                if (isTrapped_[info.X, info.Y])
                {
                    toBeKickedClients.Add(id);
                }
                else
                {
                    if (placedFlags_[info.X, info.Y] == 0)
                        placedFlags_[info.X, info.Y] = id;
                }
            }
        }

        if (CountFilledTiles() == MapSize * MapSize)
            endScreen_ = EndScreen.Create(this);

        UpdateOutput output = new()
        {
            ClientsToTerminate = toBeKickedClients.ToArray()
        };

        return output;
    }

    public const int TickRateWhole = 5;
    public static float DesiredTickRate => TickRateWhole;
}

[MemoryPackable(GenerateType.CircularReference, SerializeLayout.Sequential)]
partial class PlayerInfo
{
    [MemoryPackInclude]
    public int X;

    [MemoryPackInclude]
    public int Y;
}
