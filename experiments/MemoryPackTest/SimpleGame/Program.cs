using System.Collections.Generic;
using System.Linq;
using FrameworkTest;
using FrameworkTest.Extensions;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

static class Program
{
    static readonly List<(RenderWindow window, Displayer displayer)> Windows = new();

    const int WindowWidth  = 480;
    const int WindowHeight = 270;

    const int HorizontalPadding = 3;
    const int Verticalpadding = 50;

    const int PaddedWindowWidth = WindowWidth + HorizontalPadding;
    const int PaddedWindowHeight = WindowHeight + Verticalpadding;
    
    const int ScreenWidth = 2200;

    const int Origin = 3;

    static Vector2i windowPosition_ = new(Origin, Origin);

    static Vector2i GetNextScreenPos()
    {
        if (windowPosition_.X + PaddedWindowWidth > ScreenWidth)
        {
            windowPosition_.Y += PaddedWindowHeight;
            windowPosition_.X = Origin;
        }

        var ret = windowPosition_;

        windowPosition_.X += PaddedWindowWidth;

        return ret;
    }

    static Displayer CreateWindow(string name)
    {
        Displayer displayer = new()
        {
            Name = name
        };

        RenderWindow window = new RenderWindow(Mode, name)
        {
            Position = GetNextScreenPos()
        };

        window.SetVerticalSyncEnabled(true);
        window.Closed += (sender, args) => window.Close();
        Windows.Add((window, displayer));

        return displayer;
    }

    static readonly VideoMode Mode = new(WindowWidth, WindowHeight);

    static int clientCounter_ = 0;

    static Client<PlayerInput, ServerInput, GameState> NextClient(MockServerSession serverSession)
    {
        MockClientSession clientSession = new(serverSession);


        return new(clientSession, new PlayerInputProvider(), new InputPredictor())
        {
            Displayer = CreateWindow($"Client {++clientCounter_}")
        };
    }

    static void Main(string[] args)
    {
        MockServerSession serverSession = new();

        Server<PlayerInput, ServerInput, GameState> server = new(serverSession, new ServerInputProvider())
        {
            Displayer = CreateWindow("Server")
        };

        server.RunAsync().AssureSuccess();

        var clients = from i in Enumerable.Range(0, 11) select NextClient(serverSession);

        foreach (var client in clients)
            client.RunAsync().AssureSuccess();

        bool active = true;

        while (active)
        {
            active = false;

            foreach (var (window, displayer) in Windows)
            {
                if (!window.IsOpen)
                    continue;

                active = true;

                window.DispatchEvents();
                displayer.Draw(window);
                window.Display();
            }
        }
    }
}
