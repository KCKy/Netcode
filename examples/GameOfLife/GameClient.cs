using SfmlExtensions;
using SFML.Graphics;
using SFML.System;
using System;
using SFML.Window;
using System.Net;
using Kcky.GameNewt.Client;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Transport.Default;
using Kcky.Useful;
using Microsoft.Extensions.Logging;

namespace GameOfLife;

class GameClient : GameBase
{
    readonly float unit_;
    readonly Vector2f origin_;
    readonly Grid grid_ = new()
    {
        Height = GameState.LevelHeight,
        Width = GameState.LevelWidth,
        Cell = new()
        {
            OutlineColor = new(150, 150, 150),
            OutlineThickness = 1f,
            FillColor = Color.Transparent
        }
    };

    readonly Client<ClientInput, ServerInput, GameState> gamenewtClient_;

    GameState? state_;

    public GameClient(IPEndPoint target, ILoggerFactory loggerFactory) : base("Game of Life Demo")
    {
        var size = (Vector2f)Window.Size;
        unit_ = MathF.Min(size.X / grid_.Width, size.Y / grid_.Height);
        Vector2f offset = new Vector2f(grid_.Width, grid_.Height) / 2 * unit_;
        Vector2f center = size / 2;
        origin_ = center - offset;
        grid_.Cell.Size = new(unit_, unit_);
        Window.KeyPressed += KeyPressedHandler;

        IpClientTransport transport = new(target);
        DefaultClientDispatcher dispatcher = new(transport);
        gamenewtClient_ = new(dispatcher, loggerFactory)
        {
            ClientInputProvider = ProvideClientInput,
            ServerInputPredictor = PredictServerInput,
            TargetDelta = 0.01f
        };
    }

    Direction? direction_ = null;
    bool start_ = false;
    void KeyPressedHandler(object? sender, KeyEventArgs args)
    {
        direction_ = args.Code switch
        {
            Keyboard.Key.A or Keyboard.Key.Left => Direction.Left,
            Keyboard.Key.D or Keyboard.Key.Right => Direction.Right,
            Keyboard.Key.W or Keyboard.Key.Up => Direction.Up,
            Keyboard.Key.S or Keyboard.Key.Down => Direction.Down,
            Keyboard.Key.Space => null,
            _ => direction_
        };
        start_ = args.Code switch
        {
            Keyboard.Key.Space => true,
            _ => start_
        };
    }

    ClientInput ProvideClientInput()
    {
        ClientInput input = new()
        {
            Direction = direction_,
            Start = start_
        };

        start_ = false;

        return input;
    }

    static void PredictServerInput(ref ServerInput input, GameState state)
    {
        input.CellRespawnEventSeed = 0;
    }

    protected override void Start()
    {
        gamenewtClient_.RunAsync().AssureNoFault();
        DebugInfo.Client = gamenewtClient_;
    }

    protected override void Update(float delta)
    {
        state_ = gamenewtClient_.Update();
    }

    protected override void Draw(float delta)
    {
        grid_.Draw(Window, origin_);
        state_?.Level.Draw(Window, unit_, origin_);
    }
}
