﻿using MemoryPack;
using Kcky.GameNewt;

namespace Basic;

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

[MemoryPackable]
partial class ServerInput;


[MemoryPackable(GenerateType.CircularReference, SerializeLayout.Sequential)]
partial class GameState : IGameState<ClientInput, ServerInput>
{
    [MemoryPackInclude]
    long[,] placedFlags_;

    [MemoryPackInclude]
    SortedDictionary<long, PlayerInfo> idToPlayer_;

    public SortedDictionary<long, PlayerInfo> IdToPlayer => idToPlayer_;
    public long[,] PlacedFlags => placedFlags_;

    public const int MapSize = 10;

    public GameState()
    {
        placedFlags_ = new long[MapSize, MapSize];
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

    public UpdateOutput Update(UpdateInput<ClientInput, ServerInput> updateInputs)
    {
        foreach (var clientInputInfo in updateInputs.ClientInputInfos.Span)
        {
            long id = clientInputInfo.Id;
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

            if (input.PlaceFlag && placedFlags_[info.X, info.Y] == 0)
            {
                placedFlags_[info.X, info.Y] = id;
            }
        }
        
        return UpdateOutput.Empty;
    }

    public static double DesiredTickRate => 5;
}

[MemoryPackable(GenerateType.CircularReference, SerializeLayout.Sequential)]
partial class PlayerInfo
{
    [MemoryPackInclude]
    public int X;

    [MemoryPackInclude]
    public int Y;
}