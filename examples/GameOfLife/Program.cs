using System;
using System.Net;
using Kcky.GameNewt.Client;
using Kcky.GameNewt.Dispatcher.Default;
using Kcky.GameNewt.Server;
using Kcky.GameNewt.Transport.Default;
using Serilog;
using Kcky.Useful;

namespace GameOfLife;

static class Program
{
    const int DefaultPort = 45963;
    static readonly IPEndPoint DefaultTarget = new(IPAddress.Loopback, DefaultPort);
    
    static void SetupLogging()
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().MinimumLevel.Verbose().CreateLogger();
        TaskExtensions.OnFault += (task, exc) => Log.Error("Task faulted: {Task} with exception:\n{Exception}", task, exc);
        TaskExtensions.OnCanceled += task => Log.Error("Task was wrongly cancelled: {Task}", task);
    }

    static void ClientMain()
    {
        SetupLogging();

        // Setup transport
        IPEndPoint target = Command.GetEndPoint("Enter an address to connect to: ", DefaultTarget);
        IpClientTransport transport = new(target);
        DefaultClientDispatcher dispatcher = new(transport);
        
        // Game specific implementation
        Displayer displayer = new("Game of Life Demo");
        ClientInputProvider clientInputProvider = new(displayer.Window);
        ServerInputPredictor serverInputPredictor = new();

        // Construct client
        Client<ClientInput, ServerInput, GameState> client = new(dispatcher)
        {
            ClientInputProvider = clientInputProvider,
            Displayer = displayer,
            ServerInputPredictor = serverInputPredictor
        };

        displayer.Client = client;

        // Run
        client.RunAsync().AssureSuccess();
        transport.RunAsync().AssureSuccess();
        
        while (displayer.Update())
        { }
    }

    static void ServerMain()
    {
        SetupLogging();

        // Setup transport
        int port = Command.GetPort("Enter a port to run the server on: ", DefaultPort);
        IPEndPoint endPoint = new(IPAddress.Any, port);
        IpServerTransport transport = new(endPoint);
        DefaultServerDispatcher dispatcher = new(transport);

        // Game specific implementation
        ServerInputProvider serverInputProvider = new();

        // Construct server
        Server<ClientInput, ServerInput, GameState> server = new(dispatcher)
        {
            ServerInputProvider = serverInputProvider
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
