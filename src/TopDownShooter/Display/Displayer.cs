using TopDownShooter.Game;
using Useful;
using System.Diagnostics;
using GameCommon;
using Serilog;

namespace TopDownShooter.Display;

class Displayer : SfmlDisplayer<GameState>
{
    readonly Renderer renderer_;

    readonly Lerper<IEntity> lerper_ = new();
    
    public Displayer(string name) : base(name)
    {
        renderer_ = new(this);
        lerper_.OnEntityDraw += DrawHandler;
        DebugInfo.Lerper = lerper_;
    }
    
    protected override void OnInit()
    {
        renderer_.Id = Id;
        frameStart_ = Stopwatch.GetTimestamp();
    }

    public override void AddAuthoritative(long frame, GameState gameState) { }

    readonly PooledBufferWriter<byte> predictWriter_ = new();
    long latestPredict_ = long.MinValue;
    long frameStart_;
    
    public override void AddPredict(long frame, GameState gameState)
    {
        (long previousFrameStart, frameStart_) = (frameStart_, Stopwatch.GetTimestamp());
        float lastLength = (float)Stopwatch.GetElapsedTime(previousFrameStart, frameStart_).TotalSeconds;

        if (frame <= latestPredict_)
            return;
        latestPredict_ = frame;

        foreach ((long id, IEntity entity) in gameState.GetEntities(predictWriter_))
        {
            lerper_.AddEntity(id, entity);
        }
        lerper_.NextFrame(lastLength);
    }

    readonly ILogger logger_ = Log.ForContext<Displayer>();
    
    protected override void Draw(float delta)
    {
        renderer_.StartDraw();
        lerper_.Draw(delta);
    }

    void DrawHandler(IEntity from, IEntity to, float t) => from.DrawSelf(renderer_, to, t);
}
