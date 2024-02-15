using System;
using TopDownShooter.Game;
using Useful;
using System.Diagnostics;
using GameCommon;
using SFML.System;
using SfmlExtensions;

namespace TopDownShooter.Display;

class Displayer : SfmlDisplayer<GameState>
{
    readonly Lerper<IEntity> lerper_ = new();
    
    public Displayer(string name) : base(name)
    {
        lerper_.OnEntityDraw += DrawHandler;
        DebugInfo.Lerper = lerper_;
    }

    protected override void OnInit()
    {
        frameStart_ = Stopwatch.GetTimestamp();
    }
    
    readonly PooledBufferWriter<byte> predictWriter_ = new();
    
    long latestPredict_ = long.MinValue;
    long frameStart_;

    public int GetFrameOffset() => lerper_.FramesBehind + (int)Math.Round(lerper_.CurrentFrameProgression);
    
    public override void AddPredict(long frame, GameState gameState)
    {
        (long previousFrameStart, frameStart_) = (frameStart_, Stopwatch.GetTimestamp());
        float lastLength = (float)Stopwatch.GetElapsedTime(previousFrameStart, frameStart_).TotalSeconds;

        if (frame <= latestPredict_)
            return;
        latestPredict_ = frame;

        foreach ((long id, IEntity entity) in gameState.GetEntities(predictWriter_))
            lerper_.AddEntity(id, entity);

        lerper_.NextFrame(lastLength);
    }

    Vector2f origin_ = new();
    public Vector2f Center { get; set; }

    readonly TiledBackground background_ = new(new("tile.png"));

    protected override void Draw(float delta)
    {
        origin_ = Center - (Vector2f)Window.Size * .5f;
        background_.Draw(Window, origin_);
        lerper_.Draw(delta);
    }

    void DrawHandler(IEntity from, IEntity to, float t)
    {
        from.DrawLerped(this, origin_, to, t);
    }
}
