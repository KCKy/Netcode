using System;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace TopDownShooter;

class InputProvider
{
    readonly RenderWindow window_;
    readonly Func<int> getFrameOffset_;

    public InputProvider(RenderWindow window, Func<int> getFrameOffset)
    {
        window_ = window;
        getFrameOffset_ = getFrameOffset;
        window.KeyPressed += KeyPressedHandler;
        window.KeyReleased += KeyReleasedHandler;
        window.MouseButtonPressed += MousePressedHandler;
    }

    bool left_, right_, up_, down_, shoot_, start_;
    int shootX_, shootY_;

    readonly object mutex_ = new();

    void KeyPressedHandler(object? sender, KeyEventArgs args)
    {
        lock (mutex_)
        {
            switch (args.Code)
            {
                case Keyboard.Key.A or Keyboard.Key.Left:
                    left_ = true;
                    return;
                case Keyboard.Key.D or Keyboard.Key.Right:
                    right_ = true;
                    return;
                case Keyboard.Key.W or Keyboard.Key.Up:
                    up_ = true;
                    return;
                case Keyboard.Key.S or Keyboard.Key.Down:
                    down_ = true;
                    return;
                case Keyboard.Key.Space:
                    start_ = true;
                    return;
            }
        }
    }

    void KeyReleasedHandler(object? sender, KeyEventArgs args)
    {
        switch (args.Code)
        {
            case Keyboard.Key.A or Keyboard.Key.Left:
                left_ = false;
                return;
            case Keyboard.Key.D or Keyboard.Key.Right:
                right_ = false;
                return;
            case Keyboard.Key.W or Keyboard.Key.Up:
                up_ = false;
                return;
            case Keyboard.Key.S or Keyboard.Key.Down:
                down_ = false;
                return;
        }
    }

    void MousePressedHandler(object? sender, MouseButtonEventArgs args)
    {
        switch (args.Button)
        {
            case Mouse.Button.Left:
                Vector2i center = (Vector2i)window_.Size / 2;
                Vector2i clickPoint = new(args.X, args.Y);
                Vector2i shootVector = clickPoint - center;
                shoot_ = true;
                shootX_ = shootVector.X;
                shootY_ = shootVector.Y;
                
                return;
        }
    }

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
                ShootFrameOffset = shoot_ ? getFrameOffset_() : 0
            };

            start_ = false;
            shoot_ = false;
            shootX_ = 0;
            shootY_ = 0;

            return input;
        }
    }
}
