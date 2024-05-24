using SfmlExtensions;
using SFML.System;
using System;
using System.Net;
using Kcky.GameNewt.Client;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Transport.Default;
using Kcky.Useful;
using Microsoft.Extensions.Logging;

namespace TopDownShooter;

class GameClient : GameBase
{
    static readonly float FrameLength = (float)(1 / GameState.DesiredTickRate);
    
    readonly Client<ClientInput, ServerInput, GameState> gamenewtClient_;
    readonly Lerper<IEntity> predictLerper_ = new();
    readonly Lerper<IEntity> authLerper_ = new();
    readonly TiledBackground background_ = new(new("tile.png"));
    readonly ILogger logger_;

    Vector2f origin_ = new();
    Vector2f center_ = new();
    
    int localId_;
    
    public GameClient(IPEndPoint target, ILoggerFactory loggerFactory) : base("Game of Life Demo")
    {
        logger_ = loggerFactory.CreateLogger<GameClient>();

        IpClientTransport transport = new(target);
        DefaultClientDispatcher dispatcher = new(transport);

        InputProvider provider = new(Window, GetFrameOffset);

        gamenewtClient_ = new(dispatcher, loggerFactory)
        {
            ClientInputProvider = provider.GetInput,
            TargetDelta = 0.01f,
            ClientInputPredictor = ClientInputPrediction.PredictClientInput
        };

        gamenewtClient_.OnInitialize += id => localId_ = id;
        gamenewtClient_.OnNewPredictiveState += HandleNewPredict;
        gamenewtClient_.OnNewAuthoritativeState += HandleNewAuthoritative;

        predictLerper_.OnEntityDraw += HandleInterpolatedDraw;
        authLerper_.OnEntityDraw += HandleInterpolatedDraw;

        DebugInfo.Lerper = predictLerper_;
    }

    public void InformPlayer(int id, Vector2f position)
    {
        if (localId_ == id)
            center_ = position;
    }

    readonly PooledBufferWriter<byte> predictWriter_ = new();
    void HandleNewPredict(long frame, GameState gameState)
    {
        foreach ((int id, IEntity entity) in gameState.GetEntities(predictWriter_))
            if (entity.IsPredicted(localId_))
                predictLerper_.AddEntity(id, entity);

        predictLerper_.NextFrame(FrameLength);
    }

    readonly PooledBufferWriter<byte> authWriter_ = new();
    void HandleNewAuthoritative(long frame, GameState gameState)
    {
        foreach ((int id, IEntity entity) in gameState.GetEntities(authWriter_))
            if (!entity.IsPredicted(localId_))
                authLerper_.AddEntity(id, entity);

        authLerper_.NextFrame(FrameLength);
    }

    void HandleInterpolatedDraw(IEntity from, IEntity to, float t)
    {
        from.DrawLerped(Window, origin_, to, t, this);
    }

    public int GetFrameOffset()
    {
        long predictFrame = gamenewtClient_.PredictFrame - gamenewtClient_.AuthFrame;
        int framesBehind = authLerper_.FramesBehind;
        float frameProgression = authLerper_.CurrentFrameProgression;
        
        logger_.LogTrace("Current frame offset: {PredictDiff} + {FramesBehind} + {Frame Progression}", predictFrame, framesBehind, frameProgression);
        return 1 + (int)predictFrame + framesBehind + (int)Math.Round(frameProgression);
    }

    protected override void Start()
    {
        gamenewtClient_.RunAsync().AssureNoFault();
        DebugInfo.Client = gamenewtClient_;
    }
    
    protected override void Update(float delta)
    {
        gamenewtClient_.Update();
    }
    
    protected override void Draw(float delta)
    {
        origin_ = center_ - (Vector2f)Window.Size * .5f;
        background_.Draw(Window, origin_);
        authLerper_.Draw(delta);
        predictLerper_.Draw(delta);
    }
}
