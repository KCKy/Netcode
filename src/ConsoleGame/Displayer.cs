using ConsoleGame;
using Core.Providers;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using System.Diagnostics.Metrics;
using System.Xml.Linq;

namespace TestGame
{
    class Displayer : IDisplayer<GameState>
    {
        static readonly Vector2u WindowSize = new(1280, 720);
        static readonly VideoMode Mode = new(WindowSize.X, WindowSize.Y);

        long frame_ = long.MinValue;

        public void AddAuthoritative(long frame, GameState gameState) { }

        public void AddPredict(long frame, GameState gameState)
        {
            frame_ = gameState.Frame;
        
            if (frame != gameState.Frame)
                throw new ArgumentException($"Frame and tick mismatch {frame} {gameState.Frame}.");
        }

        public void Display()
        {
            text_.DisplayedString = $"Frame: {frame_}";

            if (!window_.IsOpen)
                return;

            window_.DispatchEvents();
            window_.Clear();
            window_.Draw(text_);
            window_.Display();
        }

        readonly Text text_ = new();
        
        readonly RenderWindow window_;

        public Displayer(string name)
        {
            text_.Font = new(@"C:\Windows\Fonts\arial.ttf");;
            text_.CharacterSize = 24;
            text_.FillColor = Color.White;
            window_ = new(Mode, name);
            window_.SetVerticalSyncEnabled(true);
            window_.Closed += (sender, args) => window_.Close();
        }
    }
}
