using Core.Providers;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using TopDownShooter.Game;
using Useful;
using System.Diagnostics;
using Core.Client;
using Serilog;
using TopDownShooter.Input;

namespace TopDownShooter.Display;

class Renderer
{
    readonly Displayer displayer_;
    readonly RenderWindow window_;
    readonly CircleShape shape_ = new();
    readonly Texture backgroundTexture_;
    readonly Sprite background_;

    public long Id { get; set; }

    static readonly ILogger Logger = Log.ForContext<Displayer>();

    Vector2f origin_ = new();
    Vector2f nextCenter_ = new();

    static float GetTileOffset(float value, float size) => -value + MathF.Floor(value / size) * size;

    void DrawBackground()
    {
        var winSize = (Vector2f)window_.Size;
        var size = (Vector2f)backgroundTexture_.Size;

        float ax = GetTileOffset(origin_.X, size.X);
        float ay = GetTileOffset(origin_.Y, size.Y);

        for (float x = ax; x <= winSize.X; x += size.X)
        for (float y = ay; y <= winSize.Y; y += size.Y)
        {
            background_.Position = new(x, y);
            window_.Draw(background_);
        }
    }

    public void StartDraw()
    {   
        origin_ = nextCenter_ - (Vector2f)window_.Size * .5f;
        DrawBackground();
    }

    public void DrawPlayer(long entityId, Vector2f position, Color color, long playerId)
    {
        const int radius = 32;
        shape_.Origin = new(radius / 2, radius / 2);
        shape_.Position = position - origin_;
        shape_.FillColor = color;
        shape_.Radius = radius;
        window_.Draw(shape_);

        if (playerId == Id)
            nextCenter_ = position;

        if (position != new Vector2f(0, 0) && displayer_.FirstReaction is null)
            displayer_.FirstReaction = Stopwatch.GetTimestamp();
    }

    public Renderer(Displayer displayer)
    {
        displayer_ = displayer;
        window_ = displayer_.Window;
        backgroundTexture_ = new("tile.png");
        background_ = new(backgroundTexture_);
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
    public Client<ClientInput, ServerInput, GameState>? Client { get; set; }

    public Displayer(string name)
    {
        Window = new(Mode, name);
        Window.SetVerticalSyncEnabled(true);
        Window.Closed += (_, _) => Window.Close();
        renderer_ = new(this);
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

    public long? FirstKeypress = null;
    public long? FirstReaction = null;

    double? GetReactionTime(long? start, long? end)
    {
        if (start is not { } s || end is not { } e)
            return null;

        return Stopwatch.GetElapsedTime(s, e).TotalSeconds;
    }

    bool debug_ = true;

    public bool Update()
    {
        if (!Window.IsOpen)
            return false;

        Window.DispatchEvents();
        var delta = clock_.ElapsedTime.AsSeconds();
        clock_.Restart();

        Window.Clear();
        renderer_.StartDraw();
        lerper_.Draw(delta);

        if (debug_)
        {
            debugText_.DisplayedString = $"Draw Delta: {delta:0.00}\n" +
                                         $"Frames behind: {lerper_.FramesBehind}\n" +
                                         $"Reaction: {GetReactionTime(FirstKeypress, FirstReaction):0.00}\n" +
                                         $"Current TPS: {Client?.CurrentTps:0.00}\n" +
                                         $"Target TPS: {Client?.TargetTps:0.00}\n" +
                                         $"Current Delta: {Client?.CurrentDelta:0.00}\n" +
                                         $"Target Delta: {Client?.TargetDelta:0.00}\n";
            Window.Draw(debugText_);
        }
        Window.Display();

        return true;
    }

    void DrawHandler(IEntity from, IEntity to, float t) => from.DrawSelf(renderer_, to, t);
}
