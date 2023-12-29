using Core.Providers;
using SFML.Graphics;
using SFML.Window;
using static SFML.Window.Keyboard;

namespace TestGame;

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

    Direction? direction_ = null;
    readonly object mutex_ = new();
    bool start_ = false;

    void KeyPressedHandler(object? sender, KeyEventArgs args)
    {
        lock (mutex_)
        {
            direction_ = args.Code switch
            {
                Key.A or Key.Left => Direction.Left,
                Key.D or Key.Right => Direction.Right,
                Key.W or Key.Up => Direction.Up,
                Key.S or Key.Down => Direction.Down,
                Key.Space => null,
                _ => direction_
            };
            start_ = args.Code switch
            {
                Key.Space => true,
                _ => start_
            };
        }
    }

    void KeyReleasedHandler(object? sender, KeyEventArgs args) { }
    void MousePressedHandler(object? sender, MouseButtonEventArgs args) { }
    void MouseReleasedHandler(object? sender, MouseButtonEventArgs args) { }
    void MouseMovedHandler(object? sender, MouseMoveEventArgs args) { }

    public ClientInput GetInput()
    {
        lock (mutex_)
        {
            ClientInput input = new()
            {
                Direction = direction_,
                Start = start_
            };

            start_ = false;

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
