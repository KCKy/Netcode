using MemoryPack;
using SFML.Graphics;
using SFML.System;

namespace SnakeGame;

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

    public void Draw(RenderTarget target, float unit, Vector2f origin)
    {
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            Vector2f position = origin + new Vector2f(x, y) * unit;
            this[x, y]?.Draw(target, position, unit);
        }
    }

    public void DrawAuth(RenderTarget target, float unit, Vector2f origin)
    {
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            Vector2f position = origin + new Vector2f(x, y) * unit;
            this[x, y]?.DrawAuth(target, position, unit);
        }
    }
}
