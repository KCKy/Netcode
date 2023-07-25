using FrameworkTest;
using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SFML.System;

using Color = SFML.Graphics.Color;

public sealed class Displayer : IServerDisplayer<PlayerInput, ServerInput, GameState>, IClientDisplayer<PlayerInput, ServerInput, GameState>
{
    readonly Text text_ = new();

    const int Side = 8;

    readonly RectangleShape playerShape_ = new()
    {
        FillColor = Color.Yellow,
        Size = new Vector2f(Side, Side)
    };

    readonly object mutex_ = new();

    public required string Name { get; init; }

    KeyValuePair<long, Vector2i>[] positions_ = Array.Empty<KeyValuePair<long, Vector2i>>();

    long id_ = -1;

    public void SetId(long id)
    {
        id_ = id;
    }

    static readonly Color[] Pallete = new[]
        { Color.Red, Color.Green, Color.Blue, Color.Yellow, Color.Magenta, Color.Cyan };

    public Displayer()
    {
        text_.Font = new(@"C:\Windows\Fonts\arial.ttf");;
        text_.CharacterSize = 24;
        text_.FillColor = Color.Green;
    }

    public void AddFrame(GameState state, long frame)
    {
        string text = $"{Name}\nTick: {state.Tick}\nFrame: {frame}";
        
        positions_ = state.Positions.ToArray();

        lock (mutex_)
            text_.DisplayedString = text;
    }

    public void Draw(RenderWindow window)
    {
        window.Clear();

        Vector2u origin = window.Size / 2;

        lock (mutex_)
        {
            foreach ((long id, Vector2i position) in positions_)
            {
                playerShape_.Position = (Vector2f)(position * Side + (Vector2i)origin);
                playerShape_.FillColor = id == id_ ? Color.White : Pallete[id % Pallete.Length];

                window.Draw(playerShape_);

            }

            window.Draw(text_);
        }
    }
}
