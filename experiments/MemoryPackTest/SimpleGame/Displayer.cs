using FrameworkTest;
using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using SFML.System;
using Color = SFML.Graphics.Color;

namespace SimpleGame;

public sealed class Displayer : IServerDisplayer<PlayerInput, ServerInput, GameState>, IClientDisplayer<PlayerInput, ServerInput, GameState>
{
    readonly Text text_ = new();

    readonly RectangleShape playerShape_ = new();

    readonly object mutex_ = new();

    const float MaxSide = 12;

    public required string Name { get; init; }

    Dictionary<long, Vector2i> positions_ = new();
    Layer layer_ = new();

    long id_ = -1;

    public void SetId(long id)
    {
        id_ = id;
    }

    static readonly Color[] Pallete = new[]
        { Color.Red, Color.Green, Color.Blue, Color.Magenta, Color.Cyan };

    static readonly Color MyColor = Color.Yellow;

    static readonly Color WallColor = new Color(50, 50, 50);

    static readonly Color StoneColor = new Color(150, 150, 150);

    public Displayer()
    {
        text_.Font = new(@"C:\Windows\Fonts\arial.ttf");;
        text_.CharacterSize = 24;
        text_.FillColor = Color.White;
    }

    public void AddFrame(GameState state, long frame)
    {
        string text = $"{Name}\nTick: {state.Tick}\nFrame: {frame}\nPlayers: {state.Players.Count}\nObjects: {state.Objects.Count}";

        positions_ = state.Players;
        layer_ = state.Objects;
        
        lock (mutex_)
            text_.DisplayedString = text;
    }

    public void DrawShape(RenderWindow window, Vector2i position, Color color, float side)
    {
        Vector2u origin = window.Size / 2;
        playerShape_.Position = (Vector2f)position * side + (Vector2f)origin;
        playerShape_.FillColor = color;
        playerShape_.Size = new(side, side);
        window.Draw(playerShape_);
    }


    float side_ = MaxSide;

    public void Draw(RenderWindow window)
    {
        window.Clear();
        
        lock (mutex_)
        {
            if (side_ > 1)
            {
                foreach ((long id, (int x, int y)) in positions_)
                {
                    if (id_ != -1 && id != id_)
                        continue;

                    while (Math.Abs((x + 1) * side_) >= window.Size.X / 2)
                    {
                        side_--;
                    }
                    while (Math.Abs((y + 1) * side_) >= window.Size.Y / 2)
                    {
                        side_--;
                    }
                }
            }
            
            foreach (var (pos, obj) in layer_)
            {
                switch (obj)
                {
                    case Wall:
                        DrawShape(window, pos, WallColor, side_);
                        break;
                    case Stone:
                        DrawShape(window, pos, StoneColor, side_);
                        break;
                }
            }

            foreach ((long id, Vector2i pos) in positions_)
            {
                DrawShape(window, pos, id == id_ ? MyColor : Pallete[id % Pallete.Length], side_);
            }

            window.Draw(text_);
        }
    }
}
