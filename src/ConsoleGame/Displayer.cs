using System.Numerics;
using Core.Providers;
using MemoryPack;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace TestGame
{
    readonly struct Pallete
    {
        public required Vector3f A { get; init; }
        public required Vector3f B { get; init; }
        public required Vector3f C { get; init; }
        public required Vector3f D { get; init; }

        public Pallete() { }

        readonly float Formula(float a, float b, float c, float d, float t) => a + b * MathF.Cos(MathF.Tau * c * t + d);

        public readonly Vector3f this[float t]
        {
            get
            {
                if (t != 1f)
                    t = MathF.Truncate(MathF.Abs(t));

                float r = Formula(A.X, B.X, C.X, D.X, t);
                float g = Formula(A.Y, B.Y, C.Y, D.Y, t);
                float b = Formula(A.Z, B.Z, C.Z, D.Z, t);
                return new(r, g, b);
            }
        }
    }

    static class Vector3fExtensions
    {
        static byte ToByte(float value)
        {
            if (value >= 1f)
                return byte.MaxValue;
            if (value <= 0f)
                return byte.MinValue;
            return (byte)(value * byte.MaxValue);
        }

        public static Color ToColor(this Vector3f v) => new (ToByte(v.X), ToByte(v.Y), ToByte(v.Z));
    }

    class Displayer : IDisplayer<GameState>
    {
    
        static readonly VideoMode Mode = new(640, 360);
        static readonly Color Background = Color.Black;

        Level level_ = new(GameState.LevelWidth, GameState.LevelHeight);

        Level authLevel_ = new(GameState.LevelWidth, GameState.LevelHeight);

        long frame_ = long.MinValue;
        
        Direction? direction_ = null;
        long id_ = long.MaxValue;

        Vector2i GridSize = new(GameState.LevelWidth, GameState.LevelHeight);
    
        public void Init(long id) => id_ = id;

        public void AddAuthoritative(long frame, GameState gameState)
        {
            byte[] serializedLevel = MemoryPackSerializer.Serialize(gameState.level_);
            MemoryPackSerializer.Deserialize(serializedLevel, ref authLevel_);
        }

        public void AddPredict(long frame, GameState gameState)
        {
            frame_ = gameState.Frame;
        
            if (frame != gameState.Frame)
                throw new ArgumentException($"Frame and tick mismatch {frame} {gameState.Frame}.");

            foreach ((long id, Player player) in gameState.IdToPlayer)
            {
                if (id != id_)
                    continue;

                direction_ = player.Direction;
            }

            byte[] serializedLevel = MemoryPackSerializer.Serialize(gameState.level_);
            MemoryPackSerializer.Deserialize(serializedLevel, ref level_);
        }

        void DrawGrid()
        {
            int w = GridSize.X;
            int h = GridSize.Y;

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                gridShape_.Position = ToGrid(x, y);
                Window.Draw(gridShape_);
            }
        }

        Vector2f ToGrid(int x, int y) => origin_ + new Vector2f(x, y) * unitLength_;


        void DrawPlayerAuth(int x, int y, PlayerAvatar avatar)
        {
            Color color = PlayerPallete[avatar.Id * PlayerPalleteSpacing].ToColor();
            color.A = 100;
            player_.FillColor = color;
            player_.Position = ToGrid(x, y);
            Window.Draw(player_);
        }

        void DrawPlayer(int x, int y, PlayerAvatar avatar)
        {
            Color color = PlayerPallete[avatar.Id * PlayerPalleteSpacing].ToColor();
            player_.FillColor = color;
            player_.Position = ToGrid(x, y);
            Window.Draw(player_);
        }

        void DrawFood(int x, int y, Food food)
        {
            player_.FillColor = Color.Red;
            player_.Position = ToGrid(x, y);
            Window.Draw(player_);
        }

        void DrawLevel()
        {
            int w = GridSize.X;
            int h = GridSize.Y;

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                ILevelObject? obj = level_[x, y];

                switch (obj)
                {
                    case PlayerAvatar player:
                        DrawPlayer(x, y, player);
                        continue;
                    case Food food:
                        DrawFood(x, y, food);
                        continue;
                }

                ILevelObject? authObj = authLevel_[x, y];
                
                switch (authObj)
                {
                    case PlayerAvatar player:
                        DrawPlayerAuth(x, y, player);
                        continue;
                }

            }
        }

        public void Display()
        {
            text_.DisplayedString = $"Frame: {frame_}\nDirection: {direction_}";

            if (!Window.IsOpen)
                return;

            Window.DispatchEvents();
            Window.Clear(Background);
            DrawGrid();
            DrawLevel();
            Window.Draw(text_);
            Window.Display();
        }
        
        readonly Text text_ = new();
        readonly CircleShape player_ = new();
        
        public RenderWindow Window { get; }

        static readonly Pallete PlayerPallete = new()
        {
            A = new(.5f, .5f, .5f),
            B = new(.5f, .5f, .5f),
            C = new(1, 1, 1),
            D = new(0, .33f, .67f)
        };

        static readonly float PlayerPalleteSpacing = 0.1f;

        readonly Vector2u size_;
    
        readonly float unitLength_;
        readonly Vector2f origin_;

        static readonly Color GridLineColor = new(100, 100, 100);
        static readonly float GridLineThickness = 1f;

        readonly RectangleShape gridShape_ = new();

        public Displayer(string name)
        {
            player_.SetPointCount(16);
            text_.Font = new("LiberationMono-Regular.ttf");
            text_.CharacterSize = 24;
            text_.FillColor = Color.White;
            Window = new(Mode, name);
            size_ = Window.Size;
            unitLength_ = MathF.Min(size_.X / GridSize.X, size_.Y / GridSize.Y);

            Vector2f offset = ((Vector2f)GridSize) / 2 * unitLength_;
            Vector2f center = ((Vector2f)size_) / 2;
            origin_ = center - offset;

            gridShape_.OutlineThickness = -GridLineThickness;
            gridShape_.Size = new(unitLength_, unitLength_);
            gridShape_.FillColor = Color.Transparent;
            gridShape_.OutlineColor = GridLineColor;

            player_.Radius = unitLength_ / 2;

            Window.SetVerticalSyncEnabled(true);
            Window.Closed += (sender, args) => Window.Close();
        }
    }
}
