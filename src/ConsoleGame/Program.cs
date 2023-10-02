using System.Diagnostics;
using System.Net;
using Core.Extensions;
using DefaultTransport;
using DefaultTransport.Client;
using DefaultTransport.Server;
using DefaultTransport.TcpTransport;
using Serilog;
using Serilog.Events;
using SimpleCommandLine;

namespace TestGame;

static class Program
{
    const int Port = 15965;

    static IPEndPoint serverAddress = IPEndPoint.Parse("127.0.0.1:15965");

    const string ServerLogFile = "server.log";
    const string ClientLogFile = "client.log";

    const string OutputTemplate = "{Timestamp: HH:mm:ss.fff zzz} [{Level}] {Message}{NewLine}{Exception}";

    static readonly IPEndPoint ServerPoint = new(IPAddress.Loopback, Port);

    static void SetupLogger(string file)
    {
        if (Debugger.IsAttached)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug, outputTemplate: OutputTemplate)
                .WriteTo.File(file, rollingInterval: RollingInterval.Infinite, outputTemplate: OutputTemplate)
                .CreateLogger();
            return;
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug, outputTemplate: OutputTemplate)
            .WriteTo.File(file, rollingInterval: RollingInterval.Infinite, outputTemplate: OutputTemplate)
            .CreateLogger();
    }
    
    static async Task Main()
    {
        while (true)
        {
            switch (Command.GetCommand(() => Console.Write("Set mode ([c]lient, [s]erver): ")))
            {
                case 'c':
                    SetupLogger(ClientLogFile);
                    RunClient();
                    goto end;
                case 's':
                    SetupLogger(ServerLogFile);
                    await RunServer();
                    goto end;
                default:
                    Console.WriteLine("Unknown mode.");
                    continue;
            }
        }

        end:
        await Log.CloseAndFlushAsync();
    }

    static void RunClient()
    {
        TcpClientTransport<IMessageToClient, IMessageToServer> transport = new()
        {
            Target = serverAddress
        };

        Displayer displayer = new("Client");
        ClientInputProvider input = new(displayer.Window);

        var client = DefaultClientConstructor.Construct<ClientInput, ServerInput, GameState>(transport, input, displayer: displayer);
        client.UseChecksum = true;
        client.TraceFrameTime = true;
        client.TraceState = true;

        client.RunAsync().AssureSuccess();
        transport.Start().AssureSuccess();
        
        while (true)
            displayer.Display();
    }

    static async Task RunServer()
    {
        TcpServerTransport<IMessageToServer, IMessageToClient> transport = new(ServerPoint);
        
        var server = DefaultServerConstructor.Construct<ClientInput, ServerInput, GameState>(transport, new ServerInputProvider());
        
        server.SendChecksum = true;
        server.TraceFrameTime = true;
        server.TraceState = true;

        server.RunAsync().AssureSuccess();
        transport.Start().AssureSuccess();

        await Task.Delay(Timeout.Infinite);
    }
}
