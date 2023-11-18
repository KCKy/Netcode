using Core.Providers;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using TopDownShooter.Game;
using Useful;
using System.Diagnostics;
using Serilog;
using Serilog.Core;

namespace TopDownShooter.Display;

class Renderer
{
    readonly Color Background = Color.Black;
    
    readonly RenderWindow window_;
    readonly CircleShape Shape = new();

    public long Id { get; set; }

    static readonly ILogger logger = Log.ForContext<Displayer>();

    public void DrawPlayer(long entityId, Vector2f position, Color color)
    {
        Shape.Position = position;
        Shape.FillColor = color;
        Shape.Radius = 32;
        window_.Draw(Shape);
    }

    public Renderer(RenderWindow window)
    {
        window_ = window;
        
    }
}

class Displayer : IDisplayer<GameState>
{
    static readonly VideoMode Mode = new(960, 540);

    public RenderWindow Window { get; }
    readonly Renderer renderer_;
    readonly Lerper<IEntity> lerper_ = new();
    
    readonly Text debugText_;
    readonly Font Font = new("LiberationMono-Regular.ttf");

    public Displayer(string name)
    {
        Window = new(Mode, name);
        Window.SetVerticalSyncEnabled(true);
        Window.Closed += (_, _) => Window.Close();
        renderer_ = new(Window);
        lerper_.OnEntityDraw += DrawHandler;
        debugText_ = new()
        {
            Font = Font,
            CharacterSize = 24,
            FillColor = Color.White
        };
    }
    
    public void Init(long id)
    {
        renderer_.Id = id;
        frameStart = Stopwatch.GetTimestamp();
        clock_.Restart();
    }

    public void AddAuthoritative(long frame, GameState gameState) { }

    readonly PooledBufferWriter<byte> predictWriter_ = new();
    long latestPredict = long.MinValue;
    long frameStart;
    
    public void AddPredict(long frame, GameState gameState)
    {
        (long previousFrameStart, frameStart) = (frameStart, Stopwatch.GetTimestamp());
        float lastLength = (float)Stopwatch.GetElapsedTime(previousFrameStart, frameStart).TotalSeconds;

        if (frame <= latestPredict)
            return;
        latestPredict = frame;

        foreach ((long id, IEntity entity) in gameState.GetEntities(predictWriter_))
        {
            lerper_.AddEntity(id, entity);
        }
        lerper_.NextFrame(lastLength);
    }

    static readonly ILogger logger = Log.ForContext<Displayer>();

    readonly Clock clock_ = new();

    public bool Update()
    {
        if (!Window.IsOpen)
            return false;

        Window.DispatchEvents();
        var delta = clock_.ElapsedTime.AsSeconds();
        clock_.Restart();

        debugText_.DisplayedString = $"Delta: {delta}\nLerper: {lerper_}";
            
        Window.Clear();
        lerper_.Draw(delta);
        Window.Draw(debugText_);
        Window.Display();

        return true;
    }

    void DrawHandler(IEntity from, IEntity to, float t) => from.DrawSelf(renderer_, to, t);
}
