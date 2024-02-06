using Core.Utility;
using GameCommon;
using SFML.Graphics;
using SFML.System;
using SfmlExtensions;
using Useful;

namespace SnakeGame;

class Displayer : SfmlDisplayer<GameState>
{
    Grid grid_ = new()
    {
        Height = GameState.LevelHeight,
        Width = GameState.LevelWidth,
        Cell = new()
        {
            OutlineColor = new(150, 150, 150),
            OutlineThickness = 1f,
            FillColor = Color.Transparent
        }
    };

    Level level_ = new();
    Level authLevel_ = new();

    readonly float unit_;
    readonly Vector2f origin_;

    public Displayer(string name) : base(name)
    {
        var size = (Vector2f)Window.Size;
        unit_ = MathF.Min(size.X / grid_.Width, size.Y / grid_.Height);
        Vector2f offset = new Vector2f(grid_.Width, grid_.Height) / 2 * unit_;
        Vector2f center = size / 2;
        origin_ = center - offset;
        grid_.Cell.Size = new(unit_, unit_);
    }

    protected override void Draw(float delta)
    {
        grid_.Draw(Window, origin_);
        authLevel_.DrawAuth(Window, unit_, origin_);
        level_.Draw(Window, unit_, origin_);
    }

    void CheckState(long frame, GameState state)
    {
        if (frame != state.Frame)
            throw new ArgumentException($"Frame and tick mismatch {frame} {state.Frame}.");
    }

    readonly PooledBufferWriter<byte> authWriter_ = new();

    public override void AddAuthoritative(long frame, GameState gameState)
    {
        authWriter_.Copy(gameState.level_, ref authLevel_);
    }

    readonly PooledBufferWriter<byte> predictWriter_ = new();

    public override void AddPredict(long frame, GameState gameState)
    {
        CheckState(frame, gameState);
        predictWriter_.Copy(gameState.level_, ref level_);
    }
}
