using SFML.Graphics;
using SFML.System;

namespace SfmlExtensions;

/// <summary>
/// A rectangular grid of given size for rendering.
/// </summary>
public sealed class Grid
{
    /// <summary>
    /// The shape used for rendering the grid.
    /// <remarks>
    /// Owned by the grid, may be modified.
    /// </remarks>
    /// </summary>
    public required RectangleShape Cell { get; set; }

    /// <summary>
    /// The horizontal number of grid cells.
    /// </summary>
    public int Width { get; set; } = 10;
    
    /// <summary>
    /// The vertical number of grid cells.
    /// </summary>
    public int Height { get; set; } = 10;

    /// <summary>
    /// Draw the grid right-down from the origin.
    /// </summary>
    /// <param name="target">The rendering target.</param>
    /// <param name="origin">The position the grid shall be rendered from.</param>
    public void Draw(RenderTarget target, Vector2f origin)
    {
        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            Cell.Position = origin + new Vector2f(x * Cell.Size.X, y * Cell.Size.Y);
            target.Draw(Cell);
        }
    }
}
