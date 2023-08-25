using FrameworkTest;
using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using FrameworkTest.Extensions;
using SFML.System;
using Color = SFML.Graphics.Color;

namespace SimpleGame;

public sealed class Displayer : IServerDisplayer<PlayerInput, ServerInput, GameState>, IClientDisplayer<PlayerInput, ServerInput, GameState>
{
    readonly Text text_ = new();

    readonly RectangleShape playerShape_ = new();

    const float MaxSide = 12;

    public required string Name { get; init; }

    long id_ = -1;

    public void SetId(long id)
    {
        id_ = id;
    }

    static readonly Color[] Pallete = new[]
        { Color.Red, Color.Green, Color.Blue, Color.Magenta, Color.Cyan };

    static readonly Color MyColor = Color.Yellow;

    static readonly Color WallColor = new (50, 50, 50);

    static readonly Color StoneColor = new (150, 150, 150);

    readonly Vector2u windowSize_;

    KeyValuePair<long, Vector2i>[] players_ = Array.Empty<KeyValuePair<long, Vector2i>>();
    //Layer layer_ = new();
    
    public Displayer(Vector2u windowSize)
    {
        text_.Font = new(@"C:\Windows\Fonts\arial.ttf");;
        text_.CharacterSize = 24;
        text_.FillColor = Color.White;
        windowSize_ = windowSize;
    }

    long frame_ = -1;
    long count_ = 0;

    public void AddFrame(GameState state, long frame)
    {
        frame_ = frame;
        
        if (frame != state.Tick)
            throw new Exception("Frame and tick mismatch.");

        count_ = state.Objects.Count;

        players_ = state.Players.ToArray();
        //layer_ = state.Objects.MemoryPackCopy();
    }

    public void DrawShape(RenderWindow target, Vector2i position, Color color, float side)
    {
        Vector2u origin = target.Size / 2;
        playerShape_.Position = (Vector2f)position * side + (Vector2f)origin;
        playerShape_.FillColor = color;
        playerShape_.Size = new(side, side);
        target.Draw(playerShape_);
    }

    float side_ = MaxSide;

    public void Draw(RenderWindow window)
    {
        text_.DisplayedString = $"{Name}\nFrame: {frame_}\nPlayers: {players_.Length}\nObjects: {count_}";

        if (side_ > 1)
        {
            foreach ((long id, (int x, int y)) in players_)
            {
                if (id_ != -1 && id != id_)
                    continue;

                while (Math.Abs((x + 1) * side_) >= windowSize_.X / 2)
                    side_--;
                
                while (Math.Abs((y + 1) * side_) >= windowSize_.Y / 2)
                    side_--;
            }
        }

        window.Clear();
        /*
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
        */

        window.Draw(text_);

        foreach ((long id, Vector2i pos) in players_)
            DrawShape(window, pos, id == id_ ? MyColor : Pallete[id % Pallete.Length], side_);
    }
}
