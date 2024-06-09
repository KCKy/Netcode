using MemoryPack;
using Kcky.GameNewt;
using Microsoft.Extensions.Logging;

namespace Basic;

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
partial class ServerInput;

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
    public const int MapSize = 10;

    public GameState()
    {
        IdToPlayer = new();
        PlacedFlags = new int[MapSize, MapSize];
    }

    public static float DesiredTickRate => 5;
    
    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs, ILogger logger)
    {
        foreach (var inputInfo in updateInputs.ClientInputInfos.Span)
        {
            int id = inputInfo.Id;
            
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

            if (inputInfo.Input.PlaceFlag && PlacedFlags[playerInfo.X, playerInfo.Y] == 0)
            {
                PlacedFlags[playerInfo.X, playerInfo.Y] = id;
            }
        }
        
        return UpdateOutput.Empty;
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
