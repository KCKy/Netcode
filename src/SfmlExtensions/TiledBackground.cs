using SFML.Graphics;
using SFML.System;

namespace SfmlExtensions;

/// <summary>
/// An infinitely repeating background of tiles. 
/// </summary>
public sealed class TiledBackground
{
    readonly Texture backgroundTexture_;
    readonly Sprite background_;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="tile">Texture of the tile.</param>
    public TiledBackground(Texture tile)
    {
        backgroundTexture_ = tile;
        background_ = new(backgroundTexture_);
    }

    static float GetTileOffset(float value, float size) => -value + MathF.Floor(value / size) * size;

    /// <summary>
    /// Draw background in the viewport.
    /// </summary>
    /// <param name="target">The rendering target.</param>
    /// <param name="origin">Where the origin of world coordinates lies in the view space.</param>
    public void Draw(RenderTarget target, Vector2f origin)
    {
        var winSize = (Vector2f)target.Size;
        var size = (Vector2f)backgroundTexture_.Size;

        float ax = GetTileOffset(origin.X, size.X);
        float ay = GetTileOffset(origin.Y, size.Y);

        for (float x = ax; x <= winSize.X; x += size.X)
        for (float y = ay; y <= winSize.Y; y += size.Y)
        {
            background_.Position = new(x, y);
            target.Draw(background_);
        }
    }
}
