using System;
using Kcky.GameNewt.Providers;
using Serilog;
using SFML.Graphics;
using SFML.System;
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

    bool left_, right_, up_, down_, shoot_, start_;
    int shootX_, shootY_;

    readonly object mutex_ = new();

    readonly ILogger logger_ = Log.ForContext<ClientInputProvider>();

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
        }
    }

    void MousePressedHandler(object? sender, MouseButtonEventArgs args)
    {
        switch (args.Button)
        {
            case Mouse.Button.Left:
                Vector2i center = (Vector2i)displayer_.Window.Size / 2;
                Vector2i clickPoint = new(args.X, args.Y);
                Vector2i shootVector = clickPoint - center;
                shoot_ = true;
                shootX_ = shootVector.X;
                shootY_ = shootVector.Y;
                
                logger_.Information("Shooting at pos {Position}", shootVector);
                return;
        }
    }

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
                Start = start_,
                Shoot = shoot_,
                ShootX = shootX_,
                ShootY = shootY_,
                ShootFrameOffset = shoot_ ? displayer_.GetFrameOffset() : 0
            };

            start_ = false;
            shoot_ = false;
            shootX_ = 0;
            shootY_ = 0;

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
