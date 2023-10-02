using System.Runtime.CompilerServices;
using HashDepot;
using MemoryPack;
using SFML.System;

namespace TestGame;

[MemoryPackable]
partial struct Level
{
    [MemoryPackConstructor]
    public Level()
    {
        objects_ = Array.Empty<ILevelObject>();
        Width = 0;
        Height = 0;
    }

    [MemoryPackInclude] ILevelObject?[] objects_;
    public int Width { get; private set; }
    public int Height { get; private set; }
    
    public Level(int width, int height)
    {
        Width = width;
        Height = height;
        int length = width * height;
        objects_ = new ILevelObject[length];
    }

    readonly void CheckValid(int x, int y)
    {
        if (x  < 0 || x >= Width)
            throw new ArgumentOutOfRangeException(nameof(x), x, "Invalid x coordinate.");
        if (y  < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(y), y, "Invalid x coordinate.");
    }

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public bool InBounds(Vector2i pos) => InBounds(pos.X, pos.Y);

    public ref ILevelObject? this[int x, int y]
    {
        get
        {
            CheckValid(x, y);
            return ref objects_[x + y * Width];
        }
    }

    public ref ILevelObject? this [Vector2i pos] => ref this[pos.X, pos.Y];
}
