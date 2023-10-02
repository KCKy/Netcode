using MemoryPack;
using SFML.System;

namespace TestGame;


static class DirectionExtensions
{
    public static Vector2i MapToMovement(this Direction direction)
    {
        return direction switch
        {
            Direction.Left => new(-1, 0),
            Direction.Right => new(1, 1),
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

struct FoodSpawnEvent
{
    public int X;
    public int Y;
    public FoodType Type;

    public FoodSpawnEvent()
    {
        X = 0;
        Y = 0;
        Type = default;
    }
}

[MemoryPackable]
partial class ServerInput
{
    public FoodSpawnEvent? FoodSpawnEvent = null;
}
