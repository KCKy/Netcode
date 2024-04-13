using System.Net;
using TopDownShooter.Game;
using TopDownShooter.Input;
using TopDownShooter.Display;
using Kcky.Useful;
using Kcky.GameNewt.Client;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport.Default;
using Serilog;
using System;
using Serilog.Events;

namespace TopDownShooter;

static class Program
{
    const int DefaultPort = 45963;
    static readonly IPEndPoint DefaultTarget = new(IPAddress.Loopback, DefaultPort);

    static void SetupLogging()
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug).WriteTo.File("log.txt").MinimumLevel.Verbose().CreateLogger();
        TaskExtensions.OnFault += (task, exc) => Log.Error("Task faulted: {Task} with exception:\n{Exception}", task, exc);
        TaskExtensions.OnCanceled += task => Log.Error("Task was wrongly cancelled: {Task}", task);
    }

    static void ClientMain()
    {
        SetupLogging();

        IPEndPoint target = Command.GetEndPoint("Enter an address to connect to: ", DefaultTarget);
        IpClientTransport transport = new(target);
        DefaultClientDispatcher dispatcher = new(transport);
        
        Displayer displayer = new("Top Down Shooter Demo");

        Client<ClientInput, ServerInput, GameState> client = new(dispatcher)
        {
            ClientInputPredictor = new ClientInputPredictor(),
            ClientInputProvider = new ClientInputProvider(displayer),
            Displayer = displayer
        };

        displayer.Client = client;

        client.RunAsync().AssureSuccess();
        transport.RunAsync().AssureSuccess();
        
        while (displayer.Update())
        { }
    }

    static void ServerMain()
    {
        SetupLogging();

        int port = Command.GetPort("Enter a port to run the server on: ", DefaultPort);

        IPEndPoint endPoint = new(IPAddress.Any, port);

        IpServerTransport transport = new(endPoint);

        DefaultServerDispatcher dispatcher = new(transport);

        ClientInputPredictor clientInputPredictor = new();

        Server<ClientInput, ServerInput, GameState> server = new(dispatcher)
        {
            ClientInputPredictor = clientInputPredictor
        };

        server.RunAsync().AssureSuccess();
        transport.RunAsync().Wait();
    }

    static void Main()
    {
        Console.WriteLine("Run client or server [c/s]? ");
        char? command = Console.ReadLine()?.ToLower()[0];
        
        switch (command)
        {
            case 's':
                ServerMain();
                return;
            default:
                ClientMain();
                return;
        }
    }
}
