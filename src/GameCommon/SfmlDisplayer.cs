using System.Runtime.InteropServices.ComTypes;
using Core.Client;
using Core.Providers;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using Clock = SFML.System.Clock;

namespace GameCommon
{
    public abstract class SfmlDisplayer<T> : IDisplayer<T>
    {
        static readonly VideoMode Mode = new(960, 540);
        static readonly Color Background = Color.Black;
        static readonly Font DebugFont = new("LiberationMono-Regular.ttf");

        protected float Unit = 1;
        protected Vector2f Origin = new(0,0);
        protected long Id = long.MinValue;

        IClient? client_;
        public IClient? Client
        {
            get => client_;
            set
            {
                debugInfo_.Client = value;
                client_ = value;
            }
        }

        protected SfmlDisplayer(string name)
        {
            Window = new(Mode, name);
            Window.SetVerticalSyncEnabled(true);
            Window.Closed += (_, _) => Window.Close();
        }

        readonly Clock clock_ = new();

        public bool Update()
        {
            if (!Window.IsOpen)
                return false;

            Window.DispatchEvents();
            Window.Clear(Background);
            float delta = clock_.ElapsedTime.AsSeconds();
            clock_.Restart();
            Draw(delta);
            debugText_.DisplayedString = debugInfo_.Update(delta);
            Window.Draw(debugText_);
            Window.Display();

            return true;
        }

        protected abstract void Draw(float delta);

        public RenderWindow Window { get; }

        readonly Text debugText_ = new()
        {
            Font = DebugFont,
            CharacterSize = 24,
            FillColor = Color.White
        };

        protected readonly DebugInfo debugInfo_ = new();

        public void Init(long id)
        {
            Id = id;
            OnInit();
        }

        protected virtual void OnInit() { }

        public abstract void AddAuthoritative(long frame, T gameState);

        public abstract void AddPredict(long frame, T gameState);
    }
}
