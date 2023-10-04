using SFML.Graphics;
using SFML.System;

namespace TestGame;

struct Grid
{
    readonly RectangleShape gridShape_ = new()
    {
        FillColor = Color.Transparent,
        OutlineColor = new(100, 100, 100),
        OutlineThickness = 1f
    };

    public Grid()
    {
        Unit = 10;
    }

    float unit_;
    public float Unit
    {
        readonly get => unit_;
        set
        {
            unit_ = value;
            gridShape_.Size = new(value, value);
        }
    }

    public int Width { get; set; } = 10;
    public int Height { get; set; } = 10;

    public void Draw(RenderTarget target, Vector2f origin)
    {
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            gridShape_.Position = origin + new Vector2f(x, y) * Unit;
            target.Draw(gridShape_);
        }
    }
}
