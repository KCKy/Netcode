using MemoryPack;
using SFML.Graphics;
using SFML.System;
using SfmlExtensions;

namespace GameOfLife;

[MemoryPackable]
sealed partial class PlayerAvatar : ILevelObject
{
    public long Id;

    static readonly Palette PlayerPalette = new()
    {
        A = new(.5f, .5f, .5f),
        B = new(.5f, .5f, .5f),
        C = new(1, 1, 1),
        D = new(.8f, .9f, .3f)
    };

    static readonly CircleShape Shape = new();

    const float PlayerPaletteSpacing = 0.1f;
    const int AuthAlpha = 100;

    static void Draw(RenderTarget target, Vector2f position, float unit, Color innerColor, Color outerColor)
    {
        Shape.FillColor = innerColor;
        Shape.OutlineColor = outerColor;
        Shape.OutlineThickness = -unit / 4;
        Shape.Radius = unit / 2;
        Shape.Position = position;
        
        target.Draw(Shape);
    }

    (Color innerColor, Color outerColor) GetColors()
    {
        var innerColor = PlayerPalette[Id * PlayerPaletteSpacing].ToColor();
        var outerColor = PlayerPalette[Id * PlayerPaletteSpacing + 0.1f].ToColor();
        return (innerColor, outerColor);
    }

    public void Draw(RenderTarget target, Vector2f position, float unit)
    {
        (Color innerColor, Color outerColor) = GetColors();
        Draw(target, position, unit, innerColor, outerColor);
    }

    public void DrawAuth(RenderTarget target, Vector2f position, float unit)
    {
        (Color innerColor, Color outerColor) = GetColors();
        innerColor.A = AuthAlpha;
        outerColor.A = AuthAlpha;

        Draw(target, position, unit, innerColor, outerColor);
    }
}
