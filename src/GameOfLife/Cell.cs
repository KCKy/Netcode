using MemoryPack;
using SFML.Graphics;
using SFML.System;

namespace GameOfLife;

[MemoryPackable]
sealed partial class Cell : ILevelObject
{
    static readonly RectangleShape AuthShape = new()
    {
        FillColor = new(100, 100, 200, 150)
    };

    static readonly RectangleShape PredictShape = new()
    {
        FillColor = new(200, 200, 200, 180)
    };

    public enum CellState : byte
    {
        Newborn, // Does not exist in current frame, created in the next
        Alive,   
        Dying  // Will die in the next frame
    }

    public CellState State = CellState.Newborn;

    public void Draw(RenderTarget target, Vector2f position, float unit)
    {
        PredictShape.Size = new(unit, unit);
        PredictShape.Position = position;
        target.Draw(PredictShape);
    }

    public void DrawAuth(RenderTarget target, Vector2f position, float unit)
    {
        AuthShape.Size = new(unit, unit);
        AuthShape.Position = position;
        target.Draw(AuthShape);
    }
}
