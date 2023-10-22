using Core.Providers;
using Core.Utility;
using SFML.Graphics;
using SFML.System;
using SFML.Window;
using Useful;

namespace TestGame;

class Displayer : IDisplayer<GameState>
{
    static readonly VideoMode Mode = new(960, 540);
    static readonly Color Background = Color.Black;
    static readonly Font Font = new("LiberationMono-Regular.ttf");

    public RenderWindow Window { get; }

    readonly float unit_;
    readonly Vector2f origin_;

    readonly Text debugText_ = new()
    {
        Font = Font,
        CharacterSize = 24,
        FillColor = Color.White
    };

    Grid grid_ = new()
    {
        Height = GameState.LevelHeight,
        Width = GameState.LevelWidth
    };
    
    // Displayed data

    Level level_ = new();
    Level authLevel_ = new();
    
    long predictFrame_ = long.MinValue;
    long authFrame_ = long.MinValue;
    Direction? direction_ = null;
    long id_ = long.MaxValue;

    //

    public Displayer(string name)
    {
        Window = new(Mode, name);
        
        var size = (Vector2f)Window.Size;
        unit_ = MathF.Min(size.X / GameState.LevelWidth, size.Y / GameState.LevelHeight);
        Vector2f offset = new Vector2f(GameState.LevelWidth, GameState.LevelHeight) / 2 * unit_;
        Vector2f center = size / 2;
        origin_ = center - offset;

        grid_.Unit = unit_;
        
        Window.SetVerticalSyncEnabled(true);
        Window.Closed += (_, _) => Window.Close();
    }
    
    public void Update()
    {
        if (!Window.IsOpen)
            return;

        Window.DispatchEvents();
        Draw();
        Window.Display();
    }

    void Draw()
    {
        debugText_.DisplayedString = $"Pred: {predictFrame_}\nAuth: {authFrame_}\nDelta: {predictFrame_ - authFrame_}\nDir: {direction_}";
        
        Window.Clear(Background);
        grid_.Draw(Window, origin_);
        authLevel_.DrawAuth(Window, unit_, origin_);
        level_.Draw(Window, unit_, origin_);
        Window.Draw(debugText_);
    }

    public void Init(long id) => id_ = id;

    void CheckState(long frame, GameState state)
    {
        if (frame != state.Frame)
            throw new ArgumentException($"Frame and tick mismatch {frame} {state.Frame}.");
    }

    PooledBufferWriter<byte> authWriter_ = new();

    public void AddAuthoritative(long frame, GameState gameState)
    {
        authFrame_ = frame;
        
        CheckState(frame, gameState);

        authWriter_.Copy(gameState.level_, ref authLevel_);
    }

    PooledBufferWriter<byte> predictWriter_ = new();

    public void AddPredict(long frame, GameState gameState)
    {
        CheckState(frame, gameState);

        predictFrame_ = gameState.Frame;

        foreach ((long id, Player player) in gameState.IdToPlayer)
        {
            if (id != id_)
                continue;

            direction_ = player.Direction;
            break;
        }
        
        predictWriter_.Copy(gameState.level_, ref level_);
    }
}
