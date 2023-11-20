using System.Diagnostics;
using Core.Providers;
using Serilog;
using SFML.Graphics;
using SFML.Window;
using TopDownShooter.Display;
using static SFML.Window.Keyboard;

namespace TopDownShooter.Input;

class ClientInputProvider : IClientInputProvider<ClientInput>, IDisposable
{
    readonly Displayer displayer_;

    public ClientInputProvider(Displayer displayer)
    {
        displayer_ = displayer;
        RenderWindow window = displayer_.Window;
        window.KeyPressed += KeyPressedHandler;
        window.KeyReleased += KeyReleasedHandler;
        window.MouseButtonPressed += MousePressedHandler;
        window.MouseButtonReleased += MouseReleasedHandler;
        window.MouseMoved += MouseMovedHandler;
    }


    bool left_, right_, up_, down_, start_;

    readonly object mutex_ = new();

    static readonly ILogger logger = Log.ForContext<ClientInputProvider>();

    void KeyPressedHandler(object? sender, KeyEventArgs args)
    {
        lock (mutex_)
        {
            switch (args.Code)
            {
                case Key.A or Key.Left:
                    left_ = true;
                    return;
                case Key.D or Key.Right:
                    right_ = true;
                    return;
                case Key.W or Key.Up:
                    up_ = true;
                    return;
                case Key.S or Key.Down:
                    down_ = true;
                    return;
                case Key.Space:
                    start_ = true;
                    return;
            }
        }
    }

    void KeyReleasedHandler(object? sender, KeyEventArgs args)
    {
        switch (args.Code)
        {
            case Key.A or Key.Left:
                left_ = false;
                return;
            case Key.D or Key.Right:
                right_ = false;
                return;
            case Key.W or Key.Up:
                up_ = false;
                return;
            case Key.S or Key.Down:
                down_ = false;
                return;
            case Key.Space:
                start_ = false;
                return;
        }
    }

    void MousePressedHandler(object? sender, MouseButtonEventArgs args) { }
    void MouseReleasedHandler(object? sender, MouseButtonEventArgs args) { }
    void MouseMovedHandler(object? sender, MouseMoveEventArgs args) { }

    public ClientInput GetInput()
    {
        lock (mutex_)
        {
            int horizontal = Convert.ToInt32(right_) - Convert.ToInt32(left_); 
            int vertical = Convert.ToInt32(down_) - Convert.ToInt32(up_); 

            ClientInput input = new()
            {
                Vertical = (sbyte)vertical,
                Horizontal = (sbyte)horizontal,
                Start = start_
            };

            if ((horizontal != 0 || vertical != 0) && displayer_.FirstKeypress is null)
                displayer_.FirstKeypress = Stopwatch.GetTimestamp();

            return input;
        }
    }

    public void Dispose()
    {
        RenderWindow window = displayer_.Window;
        window.KeyPressed -= KeyPressedHandler;
        window.KeyReleased -= KeyReleasedHandler;
        window.MouseButtonPressed -= MousePressedHandler;
        window.MouseButtonReleased -= MouseReleasedHandler;
        window.MouseMoved -= MouseMovedHandler;
    }
}
