using MemoryPack;
using SFML.System;

namespace GameOfLife;

static class DirectionExtensions
{
    public static Vector2i MapToMovement(this Direction direction)
    {
        return direction switch
        {
            Direction.Left => new(-1, 0),
            Direction.Right => new(1, 0),
            Direction.Up => new(0, -1),
            Direction.Down => new(0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Invalid direction value.")
        };
    }
}

enum Direction : byte
{
    Right = 0,
    Up,
    Left,
    Down
}

[MemoryPackable]
partial class ClientInput
{
    public Direction? Direction = null;
    public bool Start = false;
}

[MemoryPackable]
partial class ServerInput
{
    public int CellRespawnEventSeed = 0;
}
