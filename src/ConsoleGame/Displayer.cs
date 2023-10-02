using Core.Providers;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace TestGame
{
    class Displayer : IDisplayer<GameState>
    {
        static readonly Vector2u WindowSize = new(1280, 720);
        static readonly VideoMode Mode = new(WindowSize.X, WindowSize.Y);

        long frame_ = long.MinValue;
        Direction? direction_ = null;

        long id_ = long.MaxValue;
        
        public void Init(long id)
        {
            id_ = id;
        }

        public void AddAuthoritative(long frame, GameState gameState) { }

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
        }

        public void Display()
        {
            text_.DisplayedString = $"Frame: {frame_}\nDirection: {direction_}";

            if (!Window.IsOpen)
                return;

            Window.DispatchEvents();
            Window.Clear();
            Window.Draw(text_);
            Window.Display();
        }
        
        readonly Text text_ = new();
        
        public RenderWindow Window { get; }

        public Displayer(string name)
        {
            text_.Font = new(@"C:\Windows\Fonts\arial.ttf");;
            text_.CharacterSize = 24;
            text_.FillColor = Color.White;
            Window = new(Mode, name);
            Window.SetVerticalSyncEnabled(true);
            Window.Closed += (sender, args) => Window.Close();
        }
    }
}
