using FrameworkTest;
using SFML.Graphics;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.ExceptionServices;
using SFML.System;

using Color = SFML.Graphics.Color;

public sealed class Displayer : IServerDisplayer<PlayerInput, ServerInput, GameState>, IClientDisplayer<PlayerInput, ServerInput, GameState>
{
    readonly Text text_ = new();


    static readonly Vector2f Offset = new Vector2f(16, 9) * Side;
    const int Side = 15;

    readonly RectangleShape playerShape_ = new()
    {
        FillColor = Color.Yellow,
        Size = new Vector2f(Side, Side)
    };

    readonly object mutex_ = new();

    public required string Name { get; init; }

    Vector2[] positions_ = Array.Empty<Vector2>();

    public Displayer()
    {
        text_.Font = new(@"C:\Windows\Fonts\arial.ttf");;
        text_.CharacterSize = 24;
        text_.FillColor = Color.Green;
    }

    public void AddFrame(GameState state, long frame)
    {
        string text = $"{Name}\nTick: {state.Tick}\nFrame: {frame}\nPos: {positions_.FirstOrDefault()}";

        var positions = state.Positions.Values;
        int size = positions.Count;

        if (size != positions_.Length)
            positions_ = new Vector2[size];

        positions.CopyTo(positions_, 0);

        lock (mutex_)
            text_.DisplayedString = text;
    }

    public void Draw(RenderWindow window)
    {
        window.Clear();

        lock (mutex_)
        {
            foreach (Vector2 position in positions_)
            {
                var pos = position * Side;
                playerShape_.Position = new Vector2f(pos.X, pos.Y) + Offset;
                window.Draw(playerShape_);
            }

            window.Draw(text_);
        }
    }
}

