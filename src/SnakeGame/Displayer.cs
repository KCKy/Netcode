using Core.Utility;
using GameCommon;
using SFML.System;
using SfmlExtensions;
using Useful;

namespace SnakeGame;

class Displayer : SfmlDisplayer<GameState>
{
    Grid grid_ = new()
    {
        Height = GameState.LevelHeight,
        Width = GameState.LevelWidth
    };

    Level level_ = new();
    Level authLevel_ = new();

    public Displayer(string name) : base(name)
    {
        var size = (Vector2f)Window.Size;
        Unit = MathF.Min(size.X / grid_.Width, size.Y / grid_.Height);
        Vector2f offset = new Vector2f(grid_.Width, grid_.Height) / 2 * Unit;
        Vector2f center = size / 2;
        Origin = center - offset;
        grid_.Unit = Unit;
    }

    protected override void Draw(float delta)
    {
        grid_.Draw(Window, Origin);
        authLevel_.DrawAuth(Window, Unit, Origin);
        level_.Draw(Window, Unit, Origin);
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
