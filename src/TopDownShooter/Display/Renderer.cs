using SFML.Graphics;
using SFML.System;
using SfmlExtensions;

namespace TopDownShooter.Display;

class Renderer
{
    readonly RenderWindow window_;
    readonly CircleShape shape_ = new();
    readonly Texture backgroundTexture_;
    readonly Sprite background_;

    public long Id { get; set; }

    Vector2f origin_ = new();
    Vector2f nextCenter_ = new();

    static float GetTileOffset(float value, float size) => -value + MathF.Floor(value / size) * size;

    void DrawBackground()
    {
        var winSize = (Vector2f)window_.Size;
        var size = (Vector2f)backgroundTexture_.Size;

        float ax = GetTileOffset(origin_.X, size.X);
        float ay = GetTileOffset(origin_.Y, size.Y);

        for (float x = ax; x <= winSize.X; x += size.X)
        for (float y = ay; y <= winSize.Y; y += size.Y)
        {
            background_.Position = new(x, y);
            window_.Draw(background_);
        }
    }

    public void StartDraw()
    {   
        origin_ = nextCenter_ - (Vector2f)window_.Size * .5f;
        DrawBackground();
    }

    static readonly Palette PlayerPalette = new()
    {
        A = new(.5f, .5f, .5f),
        B = new(.5f, .5f, .5f),
        C = new(1, 1, 1),
        D = new(.8f, .9f, .3f)
    };

    const float PlayerPaletteSpacing = 0.3f;

    public void DrawPlayer(long entityId, Vector2f position, Color color, long playerId)
    {
        const int radius = 32;
        
        shape_.Position = position - origin_;
        shape_.Origin = new (radius, radius);
        shape_.FillColor = color;
        shape_.Radius = radius;

        shape_.FillColor = PlayerPalette[playerId * PlayerPaletteSpacing].ToColor();
        window_.Draw(shape_);

        if (playerId == Id)
            nextCenter_ = position;
    }

    public Renderer(Displayer displayer)
    {
        window_ = displayer.Window;
        backgroundTexture_ = new("tile.png");
        background_ = new(backgroundTexture_);
    }
}
