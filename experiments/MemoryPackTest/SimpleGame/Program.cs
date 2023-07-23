using System.Collections.Generic;
using FrameworkTest;
using FrameworkTest.Extensions;
using SFML.Graphics;
using SFML.Window;

static class Program
{
    static readonly List<(RenderWindow window, Displayer displayer)> windows_ = new();
    
    static Displayer CreateWindow(string name)
    {
        Displayer displayer = new()
        {
            Name = name
        };

        RenderWindow window = new RenderWindow(Mode, name);
        window.SetVerticalSyncEnabled(true);
        window.Closed += (sender, args) => window.Close();
        windows_.Add((window, displayer));

        return displayer;
    }

    static readonly VideoMode Mode = new(480, 270);

    static void Main(string[] args)
    {
        MockServerSession serverSession = new();

        Server<PlayerInput, ServerInput, GameState> server = new(serverSession, new ServerInputProvider())
        {
            Displayer = CreateWindow("Server")
        };

        MockClientSession clientSession = new(serverSession);

        Client<PlayerInput, ServerInput, GameState> client = new(clientSession, new PlayerInputProvider(), new InputPredictor())
        {
            Displayer = CreateWindow("Client 1")
        };

        MockClientSession client2Session = new(serverSession);

        Client<PlayerInput, ServerInput, GameState> client2 = new(client2Session, new PlayerInputProvider(), new InputPredictor())
        {
            Displayer = CreateWindow("Client 2")
        };
        
        server.RunAsync().AssureSuccess();
        client.RunAsync().AssureSuccess();
        client2.RunAsync().AssureSuccess();

        bool active = true;

        while (active)
        {
            active = false;

            foreach (var (window, displayer) in windows_)
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
