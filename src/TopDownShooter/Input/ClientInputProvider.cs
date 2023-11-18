using Core.Providers;
using Serilog;
using SFML.Graphics;
using SFML.Window;
using static SFML.Window.Keyboard;

namespace TopDownShooter.Input;

class ClientInputProvider : IClientInputProvider<ClientInput>, IDisposable
{
    readonly RenderWindow window_;

    public ClientInputProvider(RenderWindow window)
    {
        window_ = window;
        window_.KeyPressed += KeyPressedHandler;
        window_.KeyReleased += KeyReleasedHandler;
        window_.MouseButtonPressed += MousePressedHandler;
        window_.MouseButtonReleased += MouseReleasedHandler;
        window_.MouseMoved += MouseMovedHandler;
    }

    int horizontal_;
    int vertical_;
    bool start_;
    readonly object mutex_ = new();

    static readonly ILogger logger = Log.ForContext<ClientInputProvider>();

    void KeyPressedHandler(object? sender, KeyEventArgs args)
    {
        lock (mutex_)
        {
            horizontal_ = args.Code switch
            {
                Key.A or Key.Left => -1,
                Key.D or Key.Right => 1,
                _ => horizontal_
            };

            vertical_ = args.Code switch
            {
                Key.W or Key.Up => -1,
                Key.S or Key.Down => 1,
                _ => vertical_
            };

            start_ = args.Code switch
            {
                Key.Space => true,
                _ => start_
            };
        }
    }

    void KeyReleasedHandler(object? sender, KeyEventArgs args)
    {
            horizontal_ = args.Code switch
            {
                Key.A or Key.Left or Key.D or Key.Right => 0,
                _ => horizontal_
            };

            vertical_ = args.Code switch
            {
                Key.W or Key.Up or Key.S or Key.Down => 0,
                _ => vertical_
            };

            start_ = args.Code switch
            {
                Key.Space => false,
                _ => start_
            };
    }

    void MousePressedHandler(object? sender, MouseButtonEventArgs args) { }
    void MouseReleasedHandler(object? sender, MouseButtonEventArgs args) { }
    void MouseMovedHandler(object? sender, MouseMoveEventArgs args) { }

    public ClientInput GetInput()
    {
        lock (mutex_)
        {
            ClientInput input = new()
            {
                Vertical = vertical_,
                Horizontal = horizontal_,
                Start = start_
            };

            return input;
        }
    }

    public void Dispose()
    {
        window_.KeyPressed -= KeyPressedHandler;
        window_.KeyReleased -= KeyReleasedHandler;
        window_.MouseButtonPressed -= MousePressedHandler;
        window_.MouseButtonReleased -= MouseReleasedHandler;
        window_.MouseMoved -= MouseMovedHandler;
    }
}
