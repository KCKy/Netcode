using System;
using TopDownShooter.Game;
using Useful;
using GameCommon;
using Serilog;
using SFML.System;
using SfmlExtensions;

namespace TopDownShooter.Display;

sealed class Displayer : SfmlDisplayer<GameState>
{
    readonly Lerper<IEntity> predictLerper_ = new();
    readonly Lerper<IEntity> authLerper_ = new();
    
    public Displayer(string name) : base(name)
    {
        predictLerper_.OnEntityDraw += DrawHandler;
        authLerper_.OnEntityDraw += DrawHandler;
        DebugInfo.Lerper = predictLerper_;
    }

    readonly PooledBufferWriter<byte> predictWriter_ = new();
    
    long latestPredict_ = long.MinValue;
    long latestAuth_ = long.MinValue;

    readonly ILogger logger_ = Log.ForContext<Displayer>();

    public int GetFrameOffset()
    {
        long predictFrame = Client!.PredictFrame - Client!.AuthFrame;
        int framesBehind = authLerper_.FramesBehind;
        float frameProgression = authLerper_.CurrentFrameProgression;

        logger_.Verbose("Current frame offset: {PredictDiff} + {FramesBehind} + {Frame Progression}", predictFrame, framesBehind, frameProgression);
        return 1 + (int)predictFrame + framesBehind + (int)Math.Round(frameProgression);
    }

    static readonly float FrameLength = (float)(1 / GameState.DesiredTickRate);

    public override void AddPredict(long frame, GameState gameState)
    {
        if (frame <= latestPredict_)
            return;
        latestPredict_ = frame;

        foreach ((long id, IEntity entity) in gameState.GetEntities(predictWriter_))
            if (entity.IsPredicted(Id))
                predictLerper_.AddEntity(id, entity);

        predictLerper_.NextFrame(FrameLength);
    }

    public override void AddAuthoritative(long frame, GameState gameState)
    {
        if (frame <= latestAuth_)
            return;
        latestAuth_ = frame;

        foreach ((long id, IEntity entity) in gameState.GetEntities(predictWriter_))
            if (!entity.IsPredicted(Id))
                authLerper_.AddEntity(id, entity);

        authLerper_.NextFrame(FrameLength);
    }

    Vector2f origin_ = new();
    public Vector2f Center { get; set; }

    readonly TiledBackground background_ = new(new("tile.png"));

    protected override void Draw(float delta)
    {
        origin_ = Center - (Vector2f)Window.Size * .5f;
        background_.Draw(Window, origin_);
        authLerper_.Draw(delta);
        predictLerper_.Draw(delta);
    }

    void DrawHandler(IEntity from, IEntity to, float t)
    {
        from.DrawLerped(this, origin_, to, t);
    }
}
